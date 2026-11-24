using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

/// <summary>Estun 산업용 로봇 Modbus TCP 상태 조회 및 6단계 권한 핸드셰이크 제어 클라이언트입니다.</summary>
public sealed class EstunClient : IRobotClient
{
    private const ushort HeartbeatRegisterAddress = 0;
    private const ushort SnapshotRegisterCount = 100;
    private const ushort CommandStatusRegisterAddress = 18;
    private const ushort CommandRegisterAddress = 51;
    private const ushort GlobalSpeedRegisterAddress = 52;
    private const ushort ProjectNameRegisterAddress = 53;
    private const ushort AuthorityFlagRegisterAddress = 99;
    private const short AcquireAuthorityValue = 0x0011;
    private const short ClearValue = 0x0000;
    private const ushort AuthorityReadyStatus = 0x0801;

    private readonly ITransport _transport;
    private readonly EstunOptions _options;
    private readonly EstunModbusFrameDecoder _frameDecoder = new();
    private readonly object _txIdLock = new();

    private ushort _transactionId;
    private long _lastConnectionId = -1;
    private Timer? _keepAliveTimer;
    private int _keepAliveInFlight;
    private bool _isOpen;
    private bool _isDisposed;

    /// <summary>전송 구현체와 옵션으로 Estun 클라이언트를 초기화합니다.</summary>
    public EstunClient(ITransport transport, EstunOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new EstunOptions();
    }

    /// <inheritdoc />
    public bool IsOpen => !_isDisposed && _isOpen && _transport.State == TransportState.Open;

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
                await _transport.OpenAsync(token).ConfigureAwait(false);
                _isOpen = true;
                StartKeepAliveIfEnabled();
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        StopKeepAlive();
        if (!_isOpen)
        {
            return Task.FromResult(RobotResult.Success());
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                _isOpen = false;
                await _transport.CloseAsync(token).ConfigureAwait(false);
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

    /// <summary>프로그램 시작 명령을 실행합니다.</summary>
    public Task<RobotResult> StartProgramAsync(CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(EstunCommandCode.StartProgram, null, cancellationToken);

    /// <summary>프로그램 정지 명령을 실행합니다.</summary>
    public Task<RobotResult> StopProgramAsync(CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(EstunCommandCode.StopProgram, null, cancellationToken);

    /// <summary>에러 리셋 명령을 실행합니다.</summary>
    public Task<RobotResult> ResetErrorAsync(CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(EstunCommandCode.ResetError, null, cancellationToken);

    /// <summary>지정된 이름의 프로젝트를 로드합니다.</summary>
    public Task<RobotResult> LoadProjectAsync(string projectName, CancellationToken cancellationToken = default)
    {
        byte[] projectBytes = EstunModbusRequestBuilder.BuildProjectNamePayload(projectName, _options.SwapProjectNameWriteBytes);
        return RunHandshakeCommandAsync(
            EstunCommandCode.LoadProject,
            (session, txProvider) => WriteMultipleRegistersAsync(session, txProvider(), ProjectNameRegisterAddress, projectBytes),
            cancellationToken);
    }

    /// <summary>현재 로드된 프로젝트를 해제합니다.</summary>
    public Task<RobotResult> UnregisterProjectAsync(CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(EstunCommandCode.UnregisterProject, null, cancellationToken);

    /// <summary>글로벌 속도 비율을 설정합니다.</summary>
    public Task<RobotResult> SetGlobalSpeedAsync(short speed, CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(
            EstunCommandCode.SetGlobalSpeed,
            (session, txProvider) => WriteSingleRegisterAsync(session, txProvider(), GlobalSpeedRegisterAddress, speed),
            cancellationToken);

    /// <summary>명령 상태 재시작 명령을 실행합니다.</summary>
    public Task<RobotResult> RestartCommandStatusAsync(CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(EstunCommandCode.RestartCommandStatus, null, cancellationToken);

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
    public async Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        RobotResult<EstunRobotSnapshot> snapshot = await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.IsSuccess
            ? RobotResult.Success(snapshot.Value.ToJson())
            : RobotResult.Failure<string>(snapshot.Error);
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
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isOpen = false;
        StopKeepAlive();
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

    private Task<RobotResult> RunHandshakeCommandAsync(
        EstunCommandCode command,
        Func<ITransportSession, Func<ushort>, Task>? preWriteAction,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                await _transport.RunExclusiveAsync(
                    async session =>
                    {
                        ushort NextTx() => NextTransactionId(session.ConnectionId);
                        if (preWriteAction is not null)
                        {
                            await preWriteAction(session, NextTx).ConfigureAwait(false);
                        }

                        await ExecuteSixStepHandshakeAsync(session, NextTx, command).ConfigureAwait(false);
                        return true;
                    },
                    token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    private async Task ExecuteSixStepHandshakeAsync(
        ITransportSession session,
        Func<ushort> nextTx,
        EstunCommandCode command)
    {
        // 사양서 03 §2.2 Step 1: 권한 플래그와 명령 레지스터가 모두 0인지 선행 확인
        ushort flag = await ReadSingleRegisterAsync(session, nextTx(), AuthorityFlagRegisterAddress).ConfigureAwait(false);
        if (flag != 0)
        {
            throw new RobotException(RobotErrorKind.DeviceError, "다른 클라이언트가 제어 권한을 점유 중이거나 이전 명령이 정리되지 않았습니다.");
        }

        ushort currentCommand = await ReadSingleRegisterAsync(session, nextTx(), CommandRegisterAddress).ConfigureAwait(false);
        if (currentCommand != 0)
        {
            throw new RobotException(RobotErrorKind.DeviceError, "명령 레지스터가 아직 0으로 초기화되지 않았습니다.");
        }

        // Step 2: 다운로드 권한 요청 (Reg 99 = 0x0011)
        await WriteSingleRegisterAsync(session, nextTx(), AuthorityFlagRegisterAddress, AcquireAuthorityValue).ConfigureAwait(false);

        // Step 3: 권한 획득(Reg 18 == 0x0801) 폴링 대기
        await WaitForAuthorityAsync(session, nextTx).ConfigureAwait(false);

        // Step 4~5: 명령 기록 후 100ms 대기 및 상태 확인
        await WriteSingleRegisterAsync(session, nextTx(), CommandRegisterAddress, (short)command).ConfigureAwait(false);
        await Task.Delay(_options.HandshakePollInterval, session.CancellationToken).ConfigureAwait(false);
        await ReadSingleRegisterAsync(session, nextTx(), CommandStatusRegisterAddress).ConfigureAwait(false);

        // Step 6: 권한 및 명령 레지스터 해제
        await WriteSingleRegisterAsync(session, nextTx(), AuthorityFlagRegisterAddress, ClearValue).ConfigureAwait(false);
        await WriteSingleRegisterAsync(session, nextTx(), CommandRegisterAddress, ClearValue).ConfigureAwait(false);
    }

    private async Task WaitForAuthorityAsync(ITransportSession session, Func<ushort> nextTx)
    {
        for (int attempt = 0; attempt < _options.MaxAuthorityPollAttempts; attempt++)
        {
            ushort status = await ReadSingleRegisterAsync(session, nextTx(), CommandStatusRegisterAddress).ConfigureAwait(false);
            if (status == AuthorityReadyStatus)
            {
                return;
            }

            await Task.Delay(_options.HandshakePollInterval, session.CancellationToken).ConfigureAwait(false);
        }

        throw new RobotException(
            RobotErrorKind.Timeout,
            $"Estun 컨트롤러가 {_options.MaxAuthorityPollAttempts}회 시도 내에 원격 제어 권한(0x0801)을 승인하지 않았습니다.");
    }

    private async Task<ushort> ReadSingleRegisterAsync(ITransportSession session, ushort txId, ushort address)
    {
        byte[] data = await ReadHoldingRegistersAsync(session, txId, address, 1).ConfigureAwait(false);
        return RobotBinaryBuffer.ReadUInt16BigEndian(data, 0);
    }

    private async Task<byte[]> ReadHoldingRegistersAsync(ITransportSession session, ushort txId, ushort startAddress, ushort quantity)
    {
        byte[] request = EstunModbusRequestBuilder.BuildReadHoldingRegisters(txId, _options.Station, startAddress, quantity);
        await session.SendAsync(request).ConfigureAwait(false);
        byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
        return EstunModbusResponseParser.ParseReadHoldingRegisters(response, txId, _options.Station, quantity);
    }

    private async Task WriteSingleRegisterAsync(ITransportSession session, ushort txId, ushort address, short value)
    {
        byte[] request = EstunModbusRequestBuilder.BuildWriteSingleRegister(txId, _options.Station, address, value);
        await session.SendAsync(request).ConfigureAwait(false);
        byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
        EstunModbusResponseParser.ValidateWriteSingleRegister(response, txId, _options.Station);
    }

    private async Task WriteMultipleRegistersAsync(ITransportSession session, ushort txId, ushort startAddress, byte[] registerBytes)
    {
        byte[] request = EstunModbusRequestBuilder.BuildWriteMultipleRegisters(txId, _options.Station, startAddress, registerBytes);
        await session.SendAsync(request).ConfigureAwait(false);
        byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
        EstunModbusResponseParser.ValidateWriteMultipleRegisters(response, txId, _options.Station);
    }

    private void StartKeepAliveIfEnabled()
    {
        if (_options.KeepAliveInterval <= TimeSpan.Zero)
        {
            return;
        }

        var newTimer = new Timer(OnKeepAliveTick, null, _options.KeepAliveInitialDelay, _options.KeepAliveInterval);
        Timer? previousTimer = Interlocked.Exchange(ref _keepAliveTimer, newTimer);
        previousTimer?.Dispose();
    }

    private void StopKeepAlive()
    {
        Timer? timer = Interlocked.Exchange(ref _keepAliveTimer, null);
        timer?.Dispose();
    }

    private void OnKeepAliveTick(object? state)
    {
        if (Interlocked.CompareExchange(ref _keepAliveInFlight, 1, 0) != 0)
        {
            return;
        }

        _ = PollKeepAliveSafeAsync();
    }

    private async Task PollKeepAliveSafeAsync()
    {
        try
        {
            if (_isDisposed || !IsOpen)
            {
                return;
            }

            using var cts = new CancellationTokenSource(_options.Timeout);
            await _transport.RunExclusiveAsync(
                session => ReadHoldingRegistersAsync(session, NextTransactionId(session.ConnectionId), HeartbeatRegisterAddress, 1),
                cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // 백그라운드 Keep-Alive 폴링 실패로 프로세스가 종료되지 않도록 무시 (명시적 I/O에서 연결 이상 보고)
        }
        finally
        {
            Interlocked.Exchange(ref _keepAliveInFlight, 0);
        }
    }

    private ushort NextTransactionId(long connectionId)
    {
        lock (_txIdLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _transactionId = 0;
            }

            _transactionId = (ushort)(_transactionId == 0xFFFF ? 0x0001 : _transactionId + 1);
            return _transactionId;
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
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(EstunClient));
        }
    }
}
