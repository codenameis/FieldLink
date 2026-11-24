using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

/// <summary>Estun 산업용 로봇 Modbus TCP 상태 조회 및 6단계 권한 핸드셰이크 제어 클라이언트입니다.</summary>
public sealed partial class EstunClient : IRobotClient
{
    private const ushort HeartbeatRegisterAddress = 0;
    private const ushort SnapshotRegisterCount = 100;

    private readonly ITransport _transport;
    private readonly EstunOptions _options;
    private readonly EstunModbusFrameDecoder _frameDecoder = new();
    private readonly ProtocolLifecycleGate _lifecycleGate = new();
    private readonly object _transactionIdLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;
    private Timer? _keepAliveTimer;
    private int _keepAliveInFlight;
    private int _isOpen;

    /// <summary>전송 구현체와 옵션으로 Estun 클라이언트를 초기화합니다.</summary>
    public EstunClient(ITransport transport, EstunOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new EstunOptions();
    }

    /// <inheritdoc />
    public bool IsOpen => !_lifecycleGate.IsDisposed && Volatile.Read(ref _isOpen) != 0 && _transport.State == TransportState.Open;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("Estun 클라이언트가 이미 열려 있습니다.");
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                if (!await _lifecycleGate.TryEnterAsync(token).ConfigureAwait(false))
                {
                    throw new ObjectDisposedException(nameof(EstunClient));
                }

                try
                {
                    ThrowIfDisposed();
                    if (IsOpen)
                    {
                        throw new InvalidOperationException("Estun 클라이언트가 이미 열려 있습니다.");
                    }

                    await _transport.OpenAsync(token).ConfigureAwait(false);
                    if (_lifecycleGate.IsDisposed)
                    {
                        await CloseTransportBestEffortAsync().ConfigureAwait(false);
                        throw new ObjectDisposedException(nameof(EstunClient));
                    }

                    Volatile.Write(ref _isOpen, 1);
                    StartKeepAliveIfEnabled();
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
        StopKeepAlive();
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
                    StopKeepAlive();
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

    /// <summary>레지스터 0~99(200바이트)를 일괄 조회해 로봇 상태 스냅샷을 반환합니다.</summary>
    public Task<RobotResult<EstunRobotSnapshot>> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                byte[] raw = await ReadRawSnapshotAsync(token).ConfigureAwait(false);
                return EstunSnapshotParser.ParseSnapshot(raw);
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
        return RobotErrorMapper.ExecuteAsync(ReadRawSnapshotAsync, _options.Timeout, cancellationToken);
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
                byte[] raw = await ReadRawSnapshotAsync(token).ConfigureAwait(false);
                EstunRobotSnapshot snapshot = EstunSnapshotParser.ParseSnapshot(raw);
                return snapshot.ToJson();
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return WriteStringAsync(address, Encoding.ASCII.GetString(data), cancellationToken);
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

        ThrowIfDisposed();
        EnsureOpen();

        return address.Trim() switch
        {
            nameof(EstunCommandCode.StartProgram) => StartProgramAsync(cancellationToken),
            nameof(EstunCommandCode.StopProgram) => StopProgramAsync(cancellationToken),
            nameof(EstunCommandCode.ResetError) => ResetErrorAsync(cancellationToken),
            nameof(EstunCommandCode.UnregisterProject) => UnregisterProjectAsync(cancellationToken),
            nameof(EstunCommandCode.RestartCommandStatus) => RestartCommandStatusAsync(cancellationToken),
            nameof(EstunCommandCode.LoadProject) => LoadProjectAsync(value, cancellationToken),
            nameof(EstunCommandCode.SetGlobalSpeed) => WriteGlobalSpeedStringAsync(value, cancellationToken),
            _ => Task.FromResult(RobotResult.Failure(
                new RobotError(RobotErrorKind.Unsupported, $"지원하지 않는 Estun 쓰기 명령입니다: '{address}'.")))
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_lifecycleGate.IsDisposed)
        {
            return;
        }

        Volatile.Write(ref _isOpen, 0);
        StopKeepAlive();
        _lifecycleGate.Dispose();
        _transport.Dispose();
    }

    private Task<RobotResult> WriteGlobalSpeedStringAsync(string value, CancellationToken cancellationToken)
    {
        if (!short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out short speed))
        {
            return Task.FromResult(RobotResult.Failure(
                new RobotError(RobotErrorKind.ParseError, $"속도 값('{value}')을 16비트 정수로 변환하지 못했습니다.")));
        }

        return SetGlobalSpeedAsync(speed, cancellationToken);
    }

    private Task<byte[]> ReadRawSnapshotAsync(CancellationToken cancellationToken) =>
        _transport.RunExclusiveAsync(
            session => ReadHoldingRegistersAsync(session, NextTransactionId(session.ConnectionId), HeartbeatRegisterAddress, SnapshotRegisterCount),
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
            throw new InvalidOperationException("Estun 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_lifecycleGate.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(EstunClient));
        }
    }
}
