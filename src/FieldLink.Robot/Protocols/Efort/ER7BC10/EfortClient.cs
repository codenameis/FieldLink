using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Efort.ER7BC10;

/// <summary>EFORT ER7B-C10 로봇 TCP 바이너리 상태 조회 클라이언트입니다.</summary>
public sealed class EfortClient : IRobotClient
{
    private readonly ITransport _transport;
    private readonly EfortOptions _options;
    private readonly EfortFrameDecoder _frameDecoder;
    private readonly ProtocolLifecycleGate _lifecycleGate = new();
    private readonly object _sequenceLock = new();

    private ushort _sequence;
    private long _lastConnectionId = -1;
    private int _isOpen;

    /// <summary>전송 구현체와 옵션으로 EFORT 클라이언트를 초기화합니다.</summary>
    /// <param name="transport">TCP 전송 구현체입니다.</param>
    /// <param name="options">정렬 버전 및 제한 시간 설정입니다. null이면 기본값(v2 정렬)을 사용합니다.</param>
    public EfortClient(ITransport transport, EfortOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new EfortOptions();
        _frameDecoder = new EfortFrameDecoder(_options.Version);
    }

    /// <inheritdoc />
    public bool IsOpen => !_lifecycleGate.IsDisposed && Volatile.Read(ref _isOpen) != 0 && _transport.State == TransportState.Open;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("EFORT 클라이언트가 이미 열려 있습니다.");
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                if (!await _lifecycleGate.TryEnterAsync(token).ConfigureAwait(false))
                {
                    throw new ObjectDisposedException(nameof(EfortClient));
                }

                try
                {
                    ThrowIfDisposed();
                    if (IsOpen)
                    {
                        throw new InvalidOperationException("EFORT 클라이언트가 이미 열려 있습니다.");
                    }

                    await _transport.OpenAsync(token).ConfigureAwait(false);
                    if (_lifecycleGate.IsDisposed)
                    {
                        await CloseTransportBestEffortAsync().ConfigureAwait(false);
                        throw new ObjectDisposedException(nameof(EfortClient));
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

    /// <summary>EFORT 컨트롤러의 전체 상태 스냅샷을 조회합니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    /// <returns>파싱된 상태 스냅샷 결과입니다.</returns>
    public Task<RobotResult<EfortRobotSnapshot>> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                byte[] frame = await ExchangeRawFrameAsync(token).ConfigureAwait(false);
                return EfortResponseParser.ParseSnapshot(frame, _options.Version);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(ExchangeRawFrameAsync, _options.Timeout, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                byte[] frame = await ExchangeRawFrameAsync(token).ConfigureAwait(false);
                EfortRobotSnapshot snapshot = EfortResponseParser.ParseSnapshot(frame, _options.Version);
                return snapshot.ToJson();
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        ThrowIfDisposed();
        EnsureOpen();
        return Task.FromResult(RobotResult.Failure(
            new RobotError(RobotErrorKind.Unsupported, "EFORT ER7B-C10 프로토콜은 읽기 전용이며 쓰기를 지원하지 않습니다.")));
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteStringAsync(string address, string value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return WriteRawAsync(address, Encoding.ASCII.GetBytes(value), cancellationToken);
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

    private Task<byte[]> ExchangeRawFrameAsync(CancellationToken cancellationToken) =>
        _transport.RunExclusiveAsync(
            async session =>
            {
                ushort sequence = NextSequence(session.ConnectionId);
                byte[] request = EfortRequestBuilder.BuildReadCommand(sequence, _options.Version);
                await session.SendAsync(request).ConfigureAwait(false);
                return await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
            },
            cancellationToken);

    private ushort NextSequence(long connectionId)
    {
        lock (_sequenceLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _sequence = 0;
            }

            ushort current = _sequence;
            _sequence++;
            return current;
        }
    }

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
            throw new InvalidOperationException("EFORT 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_lifecycleGate.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(EfortClient));
        }
    }
}
