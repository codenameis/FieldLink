using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Transport;

namespace FieldLink.Plc;

/// <summary>PLC와 통신하는 최상위 클라이언트 진입점입니다. (Facade / Controller)</summary>
/// <remarks>
/// <para>프로토콜과 전송 계층의 예외를 <see cref="PlcResult"/> 및 <see cref="PlcResult{T}"/>로 일관되게 변환합니다.</para>
/// <para>호출자의 인수 오류는 즉시 예외로 알립니다.</para>
/// </remarks>
public sealed partial class PlcClient : IDisposable
{
    private readonly ITransport _transport;
    private readonly IPlcProtocol _protocol;
    private readonly PlcClientOptions _options;
    private readonly ByteOrder _byteOrder;
    private readonly IPlcStringFormat _stringFormat;

    private bool _isOpen;
    private bool _isDisposed;

    /// <summary>전송과 프로토콜 구현체를 주입받아 클라이언트를 초기화합니다.</summary>
    /// <param name="transport">바이트 송수신 전송 구현체입니다.</param>
    /// <param name="protocol">PLC 프로토콜 규칙 구현체입니다.</param>
    /// <param name="options">시간 제한 및 바이트 순서 등의 사용자 설정입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transport"/> 또는 <paramref name="protocol"/>이 null일 때.</exception>
    public PlcClient(ITransport transport, IPlcProtocol protocol, PlcClientOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _options = options ?? new PlcClientOptions();
        _byteOrder = _options.ByteOrder ?? _protocol.Traits.DefaultByteOrder;
        _stringFormat = _options.StringFormat ?? _protocol.Traits.StringFormat;
    }

    /// <summary>현재 연결되어 정상 통신 가능한 상태인지 여부입니다.</summary>
    public bool IsOpen => !_isDisposed && _isOpen && _transport.State == TransportState.Open;

    /// <summary>바이트 순서 설정입니다.</summary>
    public ByteOrder ByteOrder => _byteOrder;

    /// <summary>전송 연결을 수립하고 프로토콜 초기 협상을 수행합니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>작업 성공 여부입니다.</returns>
    /// <exception cref="InvalidOperationException">이미 연결되어 있을 때.</exception>
    public async Task<PlcResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsOpen)
        {
            throw new InvalidOperationException("PLC 클라이언트가 이미 열려 있습니다.");
        }

        return await ExecuteWithResultAsync(async token =>
        {
            await _transport.OpenAsync(token).ConfigureAwait(false);
            await _protocol.ConnectAsync(token).ConfigureAwait(false);
            _isOpen = true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>프로토콜 정상 해제 및 전송 연결 닫기를 수행합니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>작업 성공 여부입니다.</returns>
    public async Task<PlcResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        if (!_isOpen)
        {
            return PlcResult.Success();
        }

        return await ExecuteWithResultAsync(async token =>
        {
            try
            {
                await _protocol.DisconnectAsync(token).ConfigureAwait(false);
            }
            finally
            {
                _isOpen = false;
                await _transport.CloseAsync(token).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isOpen = false;
        _transport.Dispose();
    }

    private async Task<PlcResult<T>> ReadCoreAsync<T>(
        string address,
        PlcDataType dataType,
        int count,
        Func<byte[], T> converter,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();

        var request = new PlcReadRequest(address, dataType, count);

        return await ExecuteWithResultAsync(async token =>
        {
            byte[] bytes = await _protocol.ReadAsync(request, token).ConfigureAwait(false);
            return converter(bytes);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PlcResult> WriteCoreAsync(
        string address,
        PlcDataType dataType,
        byte[] data,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();

        var request = new PlcWriteRequest(address, dataType, data);

        return await ExecuteWithResultAsync(async token =>
        {
            await _protocol.WriteAsync(request, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PlcResult> ExecuteWithResultAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(_options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await action(linkedCts.Token).ConfigureAwait(false);
            return PlcResult.Success();
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return PlcResult.Failure(new PlcError(PlcErrorKind.Timeout, "작업 제한 시간이 초과되었습니다.", exception: exception));
        }
        catch (TransportException exception)
        {
            return PlcResult.Failure(MapTransportError(exception));
        }
        catch (PlcDeviceException exception)
        {
            return PlcResult.Failure(new PlcError(
                PlcErrorKind.DeviceError,
                exception.Message,
                deviceErrorCode: exception.DeviceErrorCode,
                completedCount: exception.CompletedCount,
                exception: exception));
        }
        catch (PlcException exception)
        {
            return PlcResult.Failure(new PlcError(
                exception.Kind,
                exception.Message,
                completedCount: exception.CompletedCount,
                exception: exception));
        }
    }

    private async Task<PlcResult<T>> ExecuteWithResultAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(_options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            T result = await action(linkedCts.Token).ConfigureAwait(false);
            return PlcResult.Success(result);
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return PlcResult.Failure<T>(new PlcError(PlcErrorKind.Timeout, "작업 제한 시간이 초과되었습니다.", exception: exception));
        }
        catch (TransportException exception)
        {
            return PlcResult.Failure<T>(MapTransportError(exception));
        }
        catch (PlcDeviceException exception)
        {
            return PlcResult.Failure<T>(new PlcError(
                PlcErrorKind.DeviceError,
                exception.Message,
                deviceErrorCode: exception.DeviceErrorCode,
                completedCount: exception.CompletedCount,
                exception: exception));
        }
        catch (PlcException exception)
        {
            return PlcResult.Failure<T>(new PlcError(
                exception.Kind,
                exception.Message,
                completedCount: exception.CompletedCount,
                exception: exception));
        }
    }

    private static PlcError MapTransportError(TransportException exception)
    {
        PlcErrorKind kind = exception.Failure switch
        {
            TransportFailure.ConnectFailed => PlcErrorKind.ConnectionFailed,
            TransportFailure.ConnectionClosed => PlcErrorKind.ConnectionLost,
            TransportFailure.InvalidFrame => PlcErrorKind.InvalidResponse,
            _ => PlcErrorKind.ConnectionLost
        };

        return new PlcError(kind, exception.Message, exception: exception);
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("PLC 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(PlcClient));
        }
    }
}
