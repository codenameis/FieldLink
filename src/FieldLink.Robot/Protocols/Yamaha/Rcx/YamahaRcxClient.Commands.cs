using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Yamaha.Rcx;

public sealed partial class YamahaRcxClient
{
    private const int DefaultRobotNumber = 1;
    private const string MotorStatusQueryPrefix = "@?MOTOR";
    private const string ModeStatusQueryPrefix = "@?MODE";
    private const string EmergencyStatusQueryPrefix = "@?EMG";
    private const string JogXySpacedPrefix = "@ JOGXY";
    private const string JogXyCompactPrefix = "@JOGXY";

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
    public Task<RobotResult<string[]>> JogXYAsync(int axis, int robot = DefaultRobotNumber, CancellationToken cancellationToken = default) =>
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
        int expectedLines = IsTwoLineCommand(trimmed) ? TwoLineResponse : SingleLineResponse;
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
        string command = BuildWriteCommandText(address, value);
        byte[] request = YamahaRcxRequestBuilder.BuildCommand(command);
        if (IsTwoLineCommand(command.Trim()))
        {
            return SendTwoLineOkCommandAsync(request, cancellationToken);
        }

        return SendOkCommandAsync(request, cancellationToken);
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

    private static string BuildWriteCommandText(string address, string value)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return address;
        }

        if (value.StartsWith("@", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(address))
        {
            return value;
        }

        return $"{address.TrimEnd()} {value.TrimStart()}";
    }

    private static bool IsTwoLineCommand(string command) =>
        command.StartsWith(MotorStatusQueryPrefix, StringComparison.OrdinalIgnoreCase)
        || command.StartsWith(ModeStatusQueryPrefix, StringComparison.OrdinalIgnoreCase)
        || command.StartsWith(EmergencyStatusQueryPrefix, StringComparison.OrdinalIgnoreCase)
        || command.StartsWith(JogXySpacedPrefix, StringComparison.OrdinalIgnoreCase)
        || command.StartsWith(JogXyCompactPrefix, StringComparison.OrdinalIgnoreCase);
}
