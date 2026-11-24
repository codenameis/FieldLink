using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Protocols.Yamaha.Rcx;

/// <summary>YAMAHA RCX 시리즈 온라인 명령 TCP ASCII 클라이언트입니다.</summary>
public sealed class YamahaRcxClient : IRobotClient
{
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;
    private const int SingleLineResponse = 1;
    private const int TwoLineResponse = 2;
    private const string MotorStatusQueryPrefix = "@?MOTOR";
    private const string ModeStatusQueryPrefix = "@?MODE";
    private const string EmergencyStatusQueryPrefix = "@?EMG";

    private readonly ITransport _transport;
    private readonly YamahaRcxOptions _options;
    private readonly DelimitedFrameDecoder _lineDecoder;

    private bool _isOpen;
    private bool _isDisposed;

    /// <summary>전송 구현체와 옵션으로 YAMAHA RCX 클라이언트를 초기화합니다.</summary>
    public YamahaRcxClient(ITransport transport, YamahaRcxOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new YamahaRcxOptions();
        _lineDecoder = new DelimitedFrameDecoder(new[] { CarriageReturn, LineFeed }, _options.MaximumLineLength);
    }

    /// <inheritdoc />
    public bool IsOpen => !_isDisposed && _isOpen && _transport.State == TransportState.Open;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("YAMAHA RCX 클라이언트가 이미 열려 있습니다.");
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
                await _transport.CloseAsync(token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>온라인 명령을 1회 송신하고 지정된 줄 수만큼 연속으로 응답 라인을 수신합니다.</summary>
    public Task<RobotResult<string[]>> SendCommandAsync(
        string command,
        int expectedLines = 1,
        CancellationToken cancellationToken = default)
    {
        byte[] request = YamahaRcxRequestBuilder.BuildCommand(command);
        return ExchangeLinesAsync(request, expectedLines, cancellationToken);
    }

    /// <summary>프로그램을 리셋(<c>@ RESET </c>)합니다.</summary>
    public Task<RobotResult> ResetAsync(CancellationToken cancellationToken = default) =>
        SendOkCommandAsync(YamahaRcxRequestBuilder.BuildReset(), cancellationToken);

    /// <summary>프로그램을 실행(<c>@ RUN </c>)합니다.</summary>
    public Task<RobotResult> RunAsync(CancellationToken cancellationToken = default) =>
        SendOkCommandAsync(YamahaRcxRequestBuilder.BuildRun(), cancellationToken);

    /// <summary>프로그램을 정지(<c>@ STOP </c>)합니다.</summary>
    public Task<RobotResult> StopAsync(CancellationToken cancellationToken = default) =>
        SendOkCommandAsync(YamahaRcxRequestBuilder.BuildStop(), cancellationToken);

    /// <summary>지정 태스크에 프로그램을 로드(<c>@ LOAD</c>)합니다.</summary>
    public Task<RobotResult> LoadAsync(string program, int taskId, CancellationToken cancellationToken = default) =>
        SendOkCommandAsync(YamahaRcxRequestBuilder.BuildLoad(program, taskId), cancellationToken);

    /// <summary>수동 직교 조그(<c>@ JOGXY</c>)를 실행하고 2줄 응답을 반환합니다.</summary>
    public Task<RobotResult<string[]>> JogXYAsync(int axis, int robot = 1, CancellationToken cancellationToken = default) =>
        ExchangeLinesAsync(YamahaRcxRequestBuilder.BuildJogXY(axis, robot), expectedLines: TwoLineResponse, cancellationToken);

    /// <summary>모터 전원 상태(<c>@?MOTOR </c>)를 조회합니다.</summary>
    public async Task<RobotResult<YamahaRcxMotorStatus>> ReadMotorStatusAsync(CancellationToken cancellationToken = default)
    {
        RobotResult<int> status = await ReadTwoLineIntAsync(YamahaRcxRequestBuilder.BuildReadMotorStatus(), cancellationToken).ConfigureAwait(false);
        if (!status.IsSuccess)
        {
            return RobotResult.Failure<YamahaRcxMotorStatus>(status.Error);
        }

        if (!Enum.IsDefined(typeof(YamahaRcxMotorStatus), status.Value))
        {
            return RobotResult.Failure<YamahaRcxMotorStatus>(
                new RobotError(RobotErrorKind.InvalidResponse, $"정의되지 않은 YAMAHA RCX 모터 상태 코드입니다: {status.Value}"));
        }

        return RobotResult.Success((YamahaRcxMotorStatus)status.Value);
    }

    /// <summary>운전 모드 상태(<c>@?MODE </c>)를 조회합니다.</summary>
    public Task<RobotResult<int>> ReadModeStatusAsync(CancellationToken cancellationToken = default) =>
        ReadTwoLineIntAsync(YamahaRcxRequestBuilder.BuildReadModeStatus(), cancellationToken);

    /// <summary>현재 관절 좌표(<c>@?WHERE </c>)를 조회합니다.</summary>
    public Task<RobotResult<float[]>> ReadJointsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        byte[] request = YamahaRcxRequestBuilder.BuildReadJoints();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                string[] lines = await ExchangeLinesCoreAsync(request, expectedLines: SingleLineResponse, token).ConfigureAwait(false);
                return YamahaRcxResponseParser.ParseJoints(lines[0]);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>비상정지 상태(<c>@?EMG </c>)를 조회합니다.</summary>
    public Task<RobotResult<int>> ReadEmergencyStatusAsync(CancellationToken cancellationToken = default) =>
        ReadTwoLineIntAsync(YamahaRcxRequestBuilder.BuildReadEmergencyStatus(), cancellationToken);

    /// <inheritdoc />
    public async Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default)
    {
        RobotResult<string> result = await ReadStringAsync(address, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? RobotResult.Success(Encoding.ASCII.GetBytes(result.Value))
            : RobotResult.Failure<byte[]>(result.Error);
    }

    /// <inheritdoc />
    public async Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        string trimmed = address.Trim();
        int expectedLines = IsTwoLineQuery(trimmed) ? TwoLineResponse : SingleLineResponse;
        RobotResult<string[]> lines = await SendCommandAsync(address, expectedLines, cancellationToken).ConfigureAwait(false);
        if (!lines.IsSuccess)
        {
            return RobotResult.Failure<string>(lines.Error);
        }

        return RobotResult.Success(string.Join("\r\n", lines.Value));
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

        string command = string.IsNullOrWhiteSpace(value)
            ? address
            : value.StartsWith("@", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(address)
                ? value
                : $"{address.TrimEnd()} {value.TrimStart()}";
        return SendOkCommandAsync(YamahaRcxRequestBuilder.BuildCommand(command), cancellationToken);
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

    private Task<RobotResult> SendOkCommandAsync(byte[] request, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                string[] lines = await ExchangeLinesCoreAsync(request, expectedLines: SingleLineResponse, token).ConfigureAwait(false);
                YamahaRcxResponseParser.ValidateOkLine(lines[0]);
            },
            _options.Timeout,
            cancellationToken);
    }

    private Task<RobotResult<int>> ReadTwoLineIntAsync(byte[] request, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                string[] lines = await ExchangeLinesCoreAsync(request, expectedLines: TwoLineResponse, token).ConfigureAwait(false);
                return YamahaRcxResponseParser.ParseStatusInt(lines[0], lines[1]);
            },
            _options.Timeout,
            cancellationToken);
    }

    private Task<RobotResult<string[]>> ExchangeLinesAsync(
        byte[] request,
        int expectedLines,
        CancellationToken cancellationToken)
    {
        if (expectedLines <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedLines), expectedLines, "기대 응답 줄 수는 1 이상이어야 합니다.");
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            token => ExchangeLinesCoreAsync(request, expectedLines, token),
            _options.Timeout,
            cancellationToken);
    }

    private Task<string[]> ExchangeLinesCoreAsync(byte[] request, int expectedLines, CancellationToken cancellationToken) =>
        _transport.RunExclusiveAsync(
            async session =>
            {
                // 사양서 07 §3.2 & §9.1: 명령은 1회만 송신하고 기대 라인 수만큼 연속 수신
                await session.SendAsync(request).ConfigureAwait(false);

                var lines = new List<string>(expectedLines);
                for (int i = 0; i < expectedLines; i++)
                {
                    byte[] rawLine = await session.ReceiveAsync(_lineDecoder).ConfigureAwait(false);
                    string line = YamahaRcxResponseParser.StripLineTerminator(rawLine);
                    lines.Add(line);

                    // 첫 줄이 NG 오류이면 후속 줄 대기 타임아웃을 피하기 위해 즉시 조기 실패 처리
                    if (i < expectedLines - 1 && YamahaRcxResponseParser.IsErrorLine(line))
                    {
                        throw new RobotDeviceException(0, line, rawLine);
                    }
                }

                return lines.ToArray();
            },
            cancellationToken);

    private static bool IsTwoLineQuery(string command) =>
        command.StartsWith(MotorStatusQueryPrefix, StringComparison.OrdinalIgnoreCase)
        || command.StartsWith(ModeStatusQueryPrefix, StringComparison.OrdinalIgnoreCase)
        || command.StartsWith(EmergencyStatusQueryPrefix, StringComparison.OrdinalIgnoreCase);

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("YAMAHA RCX 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(YamahaRcxClient));
        }
    }
}
