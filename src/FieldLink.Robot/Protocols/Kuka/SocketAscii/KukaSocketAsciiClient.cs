using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Shared;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Protocols.Kuka.SocketAscii;

/// <summary>KUKA 커스텀 Socket ASCII TCP 명령 인터페이스 클라이언트입니다.</summary>
public sealed class KukaSocketAsciiClient : IRobotClient
{
    private readonly ITransport _transport;
    private readonly KukaSocketAsciiOptions _options;
    private readonly AvailableBytesFrameDecoder _frameDecoder;
    private readonly ProtocolLifecycleGate _lifecycleGate = new();

    private int _isOpen;

    /// <summary>전송 구현체와 옵션으로 KUKA Socket ASCII 클라이언트를 초기화합니다.</summary>
    public KukaSocketAsciiClient(ITransport transport, KukaSocketAsciiOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new KukaSocketAsciiOptions();
        _frameDecoder = new AvailableBytesFrameDecoder(_options.MaximumResponseBytes);
    }

    /// <inheritdoc />
    public bool IsOpen => !_lifecycleGate.IsDisposed && Volatile.Read(ref _isOpen) != 0 && _transport.State == TransportState.Open;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("KUKA Socket ASCII 클라이언트가 이미 열려 있습니다.");
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                if (!await _lifecycleGate.TryEnterAsync(token).ConfigureAwait(false))
                {
                    throw new ObjectDisposedException(nameof(KukaSocketAsciiClient));
                }

                try
                {
                    ThrowIfDisposed();
                    if (IsOpen)
                    {
                        throw new InvalidOperationException("KUKA Socket ASCII 클라이언트가 이미 열려 있습니다.");
                    }

                    await _transport.OpenAsync(token).ConfigureAwait(false);
                    if (_lifecycleGate.IsDisposed)
                    {
                        await CloseTransportBestEffortAsync().ConfigureAwait(false);
                        throw new ObjectDisposedException(nameof(KukaSocketAsciiClient));
                    }

                    Volatile.Write(ref _isOpen, 1);
                }
                finally
                {
                    _lifecycleGate.Exit();
                }
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _isOpen) == 0 || _lifecycleGate.IsDisposed)
        {
            return Task.FromResult(RobotResult.Success());
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                if (!await _lifecycleGate.TryEnterAsync(token).ConfigureAwait(false))
                {
                    return;
                }

                try
                {
                    if (Volatile.Read(ref _isOpen) == 0)
                    {
                        return;
                    }

                    Volatile.Write(ref _isOpen, 0);
                    await _transport.CloseAsync(token).ConfigureAwait(false);
                }
                finally
                {
                    _lifecycleGate.Exit();
                }
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>하나 이상의 변수 값을 문자열로 조회합니다.</summary>
    public Task<RobotResult<string>> ReadVariablesAsync(
        IReadOnlyList<string> addresses,
        CancellationToken cancellationToken = default)
    {
        byte[] request = KukaSocketAsciiRequestBuilder.BuildReadRequest(addresses, _options.Encoding);
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                byte[] response = await ExchangeAsync(request, token).ConfigureAwait(false);
                return KukaSocketAsciiResponseParser.ParseReadResponse(response, _options.Encoding);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>하나 이상의 변수에 값을 기록합니다.</summary>
    public Task<RobotResult> WriteVariablesAsync(
        IReadOnlyList<string> addresses,
        IReadOnlyList<string> values,
        CancellationToken cancellationToken = default)
    {
        byte[] request = KukaSocketAsciiRequestBuilder.BuildWriteRequest(addresses, values, _options.Encoding);
        return SendControlCommandAsync(request, cancellationToken);
    }

    /// <summary>지정된 이름의 로봇 프로그램을 시작합니다.</summary>
    public Task<RobotResult> StartProgramAsync(string programName, CancellationToken cancellationToken = default)
    {
        byte[] request = KukaSocketAsciiRequestBuilder.BuildStartProgramRequest(programName, _options.Encoding);
        return SendControlCommandAsync(request, cancellationToken);
    }

    /// <summary>현재 프로그램을 리셋(<c>0601</c>)합니다.</summary>
    public Task<RobotResult> ResetProgramAsync(CancellationToken cancellationToken = default)
    {
        byte[] request = KukaSocketAsciiRequestBuilder.BuildResetProgramRequest(_options.Encoding);
        return SendControlCommandAsync(request, cancellationToken);
    }

    /// <summary>현재 프로그램을 정지(<c>0621</c>)합니다.</summary>
    public Task<RobotResult> StopProgramAsync(CancellationToken cancellationToken = default)
    {
        byte[] request = KukaSocketAsciiRequestBuilder.BuildStopProgramRequest(_options.Encoding);
        return SendControlCommandAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        byte[] request = KukaSocketAsciiRequestBuilder.BuildReadRequest(new[] { address }, _options.Encoding);
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(token => ExchangeAsync(request, token), _options.Timeout, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        return ReadVariablesAsync(new[] { address }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return WriteStringAsync(address, _options.Encoding.GetString(data), cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteStringAsync(string address, string value, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return WriteVariablesAsync(new[] { address }, new[] { value }, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_lifecycleGate.IsDisposed)
        {
            return;
        }

        Volatile.Write(ref _isOpen, 0);
        _lifecycleGate.Dispose();
        _transport.Dispose();
    }

    private Task<RobotResult> SendControlCommandAsync(byte[] request, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                byte[] response = await ExchangeAsync(request, token).ConfigureAwait(false);
                KukaSocketAsciiResponseParser.ValidateControlResponse(response, _options.Encoding);
            },
            _options.Timeout,
            cancellationToken);
    }

    private Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken) =>
        _transport.RunExclusiveAsync(
            async session =>
            {
                await session.SendAsync(request).ConfigureAwait(false);
                return await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            },
            cancellationToken);

    private async Task CloseTransportBestEffortAsync()
    {
        try
        {
            await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Dispose와 OpenAsync가 겹칠 때 이미 해제된 전송 계층의 닫기 실패는 무시한다.
        }
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("KUKA Socket ASCII 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_lifecycleGate.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(KukaSocketAsciiClient));
        }
    }
}
