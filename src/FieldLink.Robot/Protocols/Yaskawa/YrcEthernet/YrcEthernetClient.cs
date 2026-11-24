using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

/// <summary>YASKAWA YRC1000 / DX200 Ethernet Server TCP 2단계 명령 클라이언트입니다.</summary>
public sealed class YrcEthernetClient : IRobotClient
{
    private const int MaximumLineLength = 8192;
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;
    private const string ErrorPrefix = "ERROR:";

    private readonly ITransport _transport;
    private readonly YrcEthernetOptions _options;
    private readonly DelimitedFrameDecoder _crLfDecoder = new(new[] { CarriageReturn, LineFeed }, MaximumLineLength);
    private readonly DelimitedFrameDecoder _crDecoder = new(new[] { CarriageReturn }, MaximumLineLength);
    private readonly FixedLengthFrameDecoder _singleByteDecoder = new(1);
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private long _connectedConnectionId = -1;
    private bool _isPersistentConnection;
    private bool _isOpen;
    private bool _isDisposed;

    /// <summary>전송 구현체와 옵션으로 YRC Ethernet TCP 클라이언트를 초기화합니다.</summary>
    public YrcEthernetClient(ITransport transport, YrcEthernetOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new YrcEthernetOptions();
    }

    /// <summary>현재 세션이 영구 연결(Keep-Alive:-1)로 협상되었는지 여부입니다.</summary>
    public bool IsPersistentConnection => _isPersistentConnection;

    /// <inheritdoc />
    public bool IsOpen => !_isDisposed && _isOpen && _transport.State == TransportState.Open;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("YRC Ethernet 클라이언트가 이미 열려 있습니다.");
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                await _transport.OpenAsync(token).ConfigureAwait(false);
                _isOpen = true;
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        if (!_isOpen)
        {
            return Task.FromResult(RobotResult.Success());
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                _isOpen = false;
                _connectedConnectionId = -1;
                await _transport.CloseAsync(token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>2단계 프로토콜로 임의 YRC 명령과 데이터를 실행합니다.</summary>
    public Task<RobotResult<string>> ExecuteCommandAsync(
        string command,
        string? commandData = null,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        ThrowIfDisposed();
        EnsureOpen();

        byte[]? dataBytes = commandData is null ? null : _options.Encoding.GetBytes(commandData);
        return RobotErrorMapper.ExecuteAsync(
            token => ExecuteTwoPhaseAsync(command, dataBytes, token),
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>활성 알람(<c>RALARM</c>)을 조회합니다.</summary>
    public Task<RobotResult<string>> ReadAlarmAsync(CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync("RALARM", null, cancellationToken);

    /// <summary>관절 좌표(<c>RPOSJ</c>)를 조회합니다.</summary>
    public Task<RobotResult<string>> ReadJointPositionAsync(CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync("RPOSJ", null, cancellationToken);

    /// <summary>직교 좌표(<c>RPOSC</c>)를 조회해 자세 모델로 반환합니다.</summary>
    public Task<RobotResult<YrcRobotPose>> ReadCartesianPositionAsync(
        int coordinateSystem = 0,
        bool includeExternalAxis = false,
        CancellationToken cancellationToken = default)
    {
        string data = $"{coordinateSystem.ToString(CultureInfo.InvariantCulture)},{(includeExternalAxis ? "1" : "0")}";
        return ExecuteCommandAndParseAsync(
            "RPOSC",
            data,
            raw => YrcRobotPose.Parse(raw, _options.ModelType),
            cancellationToken);
    }

    /// <summary>로봇 상태 16비트(<c>RSTATS</c>)를 조회합니다.</summary>
    public Task<RobotResult<bool[]>> ReadStatusBitsAsync(CancellationToken cancellationToken = default) =>
        ExecuteCommandAndParseAsync(
            "RSTATS",
            null,
            YrcEthernetResponseParser.ParseStatsBits,
            cancellationToken);

    /// <summary>현재 실행 중인 JOB/라인/스텝(<c>RJSEQ</c>)을 조회합니다.</summary>
    public Task<RobotResult<string>> ReadJobSequenceAsync(CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync("RJSEQ", null, cancellationToken);

    /// <summary>지정 사용자 좌표계(<c>RUFRAME</c>, 1~64)를 조회합니다.</summary>
    public Task<RobotResult<string>> ReadUserFrameAsync(int frameNumber, CancellationToken cancellationToken = default) =>
        ExecuteCommandAsync("RUFRAME", frameNumber.ToString(CultureInfo.InvariantCulture), cancellationToken);

    /// <summary>컨트롤러 변수(<c>SAVEV</c>)를 조회합니다.</summary>
    public Task<RobotResult<string>> ReadVariableAsync(
        YrcVariableType variableType,
        string variableAddress,
        CancellationToken cancellationToken = default)
    {
        if (variableAddress is null)
        {
            throw new ArgumentNullException(nameof(variableAddress));
        }

        return ExecuteCommandAsync("SAVEV", $"{(int)variableType},{variableAddress}", cancellationToken);
    }

    /// <summary>지정 I/O 접점을 8점 배수로 읽습니다. (<c>IOREAD</c>)</summary>
    public Task<RobotResult<bool[]>> ReadIoAsync(
        int startAddress,
        int pointCount,
        CancellationToken cancellationToken = default)
    {
        string data = YrcEthernetRequestBuilder.BuildIoReadData(startAddress, pointCount);
        return ExecuteCommandAndParseAsync(
            "IOREAD",
            data,
            raw => YrcEthernetResponseParser.ParseIoReadBits(raw, pointCount),
            cancellationToken);
    }

    /// <summary>네트워크 입력 영역에 I/O 비트를 8점 배수로 씁니다. (<c>IOWRITE</c>)</summary>
    public Task<RobotResult> WriteIoAsync(int startAddress, bool[] values, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("IOWRITE", YrcEthernetRequestBuilder.BuildIoWriteData(startAddress, values), cancellationToken);

    /// <summary>HOLD ON/OFF를 설정합니다.</summary>
    public Task<RobotResult> HoldAsync(bool on, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("HOLD", on ? "1" : "0", cancellationToken);

    /// <summary>알람을 리셋합니다.</summary>
    public Task<RobotResult> ResetAsync(CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("RESET", null, cancellationToken);

    /// <summary>에러를 취소합니다.</summary>
    public Task<RobotResult> CancelAsync(CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("CANCEL", null, cancellationToken);

    /// <summary>운전 모드를 선택합니다. (1: 교시, 2: 재생)</summary>
    public Task<RobotResult> SetModeAsync(int mode, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("MODE", mode.ToString(CultureInfo.InvariantCulture), cancellationToken);

    /// <summary>사이클 모드를 선택합니다. (1: Step, 2: 1-Cycle, 3: Auto)</summary>
    public Task<RobotResult> SetCycleAsync(int cycle, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("CYCLE", cycle.ToString(CultureInfo.InvariantCulture), cancellationToken);

    /// <summary>서보 전원을 ON/OFF 합니다.</summary>
    public Task<RobotResult> SetServoPowerAsync(bool on, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("SVON", on ? "1" : "0", cancellationToken);

    /// <summary>펜던트 및 I/O 인터락을 설정합니다.</summary>
    public Task<RobotResult> SetHoldLockAsync(bool on, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("HLOCK", on ? "1" : "0", cancellationToken);

    /// <summary>티칭 펜던트에 메시지를 표시합니다. (<c>MDSP</c>, 최대 30바이트)</summary>
    public Task<RobotResult> DisplayMessageAsync(string message, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("MDSP", YrcEthernetRequestBuilder.ValidateDisplayMessage(message, _options.Encoding), cancellationToken);

    /// <summary>지정 프로그램을 시작합니다. (null이면 현재 줄부터 실행)</summary>
    public Task<RobotResult> StartJobAsync(string? programName = null, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("START", programName, cancellationToken);

    /// <summary>지정 프로그램을 삭제합니다.</summary>
    public Task<RobotResult> DeleteJobAsync(string programName, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("DELETE", programName ?? throw new ArgumentNullException(nameof(programName)), cancellationToken);

    /// <summary>메인 JOB을 설정합니다.</summary>
    public Task<RobotResult> SetMasterJobAsync(string programName, CancellationToken cancellationToken = default) =>
        ExecuteControlAsync("SETMJ", programName ?? throw new ArgumentNullException(nameof(programName)), cancellationToken);

    /// <summary>실행 JOB 및 라인 번호를 설정합니다.</summary>
    public Task<RobotResult> SelectJobAsync(string programName, int lineNumber, CancellationToken cancellationToken = default)
    {
        if (programName is null)
        {
            throw new ArgumentNullException(nameof(programName));
        }

        return ExecuteControlAsync("JSEQ", $"{programName},{lineNumber.ToString(CultureInfo.InvariantCulture)}", cancellationToken);
    }

    /// <summary>관절 보간 이동(<c>MOVJ</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> MoveJointAsync(YrcRobotPose pose, CancellationToken cancellationToken = default)
    {
        if (pose is null)
        {
            throw new ArgumentNullException(nameof(pose));
        }

        return ExecuteControlAsync("MOVJ", pose.ToMoveCommandString(_options.ModelType), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default)
    {
        RobotResult<string> result = await ReadStringAsync(address, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? RobotResult.Success(_options.Encoding.GetBytes(result.Value))
            : RobotResult.Failure<byte[]>(result.Error);
    }

    /// <inheritdoc />
    public Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        int separatorIndex = address.IndexOfAny(new[] { '.', ':', ';' });
        if (separatorIndex >= 0)
        {
            string command = address.Substring(0, separatorIndex);
            string data = address.Substring(separatorIndex + 1);
            return ExecuteCommandAsync(command, data, cancellationToken);
        }

        return ExecuteCommandAsync(address, null, cancellationToken);
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

        return ExecuteControlAsync(address, value.Length == 0 ? null : value, cancellationToken);
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

    private Task<RobotResult<T>> ExecuteCommandAndParseAsync<T>(
        string command,
        string? commandData,
        Func<string, T> parser,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();

        byte[]? dataBytes = commandData is null ? null : _options.Encoding.GetBytes(commandData);
        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                string raw = await ExecuteTwoPhaseAsync(command, dataBytes, token).ConfigureAwait(false);
                return parser(raw);
            },
            _options.Timeout,
            cancellationToken);
    }

    private async Task<RobotResult> ExecuteControlAsync(string command, string? data, CancellationToken cancellationToken)
    {
        RobotResult<string> result = await ExecuteCommandAsync(command, data, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? RobotResult.Success() : RobotResult.Failure(result.Error);
    }

    private async Task<string> ExecuteTwoPhaseAsync(string command, byte[]? dataBytes, CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_transport.State == TransportState.Closed && _isOpen)
            {
                await _transport.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            string response = await _transport.RunExclusiveAsync(
                session => SendAndReceiveTwoPhaseAsync(session, command, dataBytes),
                cancellationToken).ConfigureAwait(false);

            if (!_isPersistentConnection)
            {
                _connectedConnectionId = -1;
                await _transport.CloseAsync(cancellationToken).ConfigureAwait(false);
            }

            return response;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task<string> SendAndReceiveTwoPhaseAsync(ITransportSession session, string command, byte[]? dataBytes)
    {
        await EnsureConnectHandshakeAsync(session).ConfigureAwait(false);

        byte[] phase1 = YrcEthernetRequestBuilder.BuildPhase1Header(command, dataBytes, _options.Encoding);
        await session.SendAsync(phase1).ConfigureAwait(false);
        byte[] phase1ReplyBytes = await session.ReceiveAsync(_crLfDecoder).ConfigureAwait(false);
        YrcEthernetResponseParser.ValidatePhase1Ack(
            YrcEthernetResponseParser.DecodeLine(phase1ReplyBytes, _options.Encoding, stripCrLf: true));

        if (dataBytes is { Length: > 0 })
        {
            await session.SendAsync(YrcEthernetRequestBuilder.BuildPhase2Payload(dataBytes)).ConfigureAwait(false);
        }

        byte[] phase3Bytes = await session.ReceiveAsync(_crDecoder).ConfigureAwait(false);
        string rawLine = YrcEthernetResponseParser.DecodeLine(phase3Bytes, _options.Encoding, stripCrLf: false);
        string parsed = YrcEthernetResponseParser.ParsePhase3Result(rawLine, out bool shouldDrainNewline);

        if (shouldDrainNewline)
        {
            await session.ReceiveAsync(_singleByteDecoder).ConfigureAwait(false);
        }

        if (rawLine.StartsWith(ErrorPrefix, StringComparison.Ordinal))
        {
            throw YrcEthernetResponseParser.ParseErrorResultException(rawLine);
        }

        return parsed;
    }

    private async Task EnsureConnectHandshakeAsync(ITransportSession session)
    {
        if (_connectedConnectionId == session.ConnectionId)
        {
            return;
        }

        await session.SendAsync(YrcEthernetRequestBuilder.BuildConnectRequest(_options.Encoding)).ConfigureAwait(false);
        byte[] replyBytes = await session.ReceiveAsync(_crLfDecoder).ConfigureAwait(false);
        string reply = YrcEthernetResponseParser.DecodeLine(replyBytes, _options.Encoding, stripCrLf: true);
        _isPersistentConnection = YrcEthernetResponseParser.ParseConnectResponse(reply);
        _connectedConnectionId = session.ConnectionId;
    }

    private void EnsureOpen()
    {
        if (!IsOpen && _transport.State != TransportState.Closed)
        {
            throw new InvalidOperationException("YRC Ethernet 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }

        if (!_isOpen)
        {
            throw new InvalidOperationException("YRC Ethernet 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(YrcEthernetClient));
        }
    }
}
