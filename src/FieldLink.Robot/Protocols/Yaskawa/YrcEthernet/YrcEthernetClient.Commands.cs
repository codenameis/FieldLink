using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

public sealed partial class YrcEthernetClient
{
    private const int MinimumCoordinateSystem = 0;
    private const int MaximumCoordinateSystem = 65;
    private const int MinimumUserFrame = 1;
    private const int MaximumUserFrame = 64;
    private const int MinimumMode = 1;
    private const int MaximumMode = 2;
    private const int MinimumCycle = 1;
    private const int MaximumCycle = 3;
    private const int MinimumJobLineNumber = 0;
    private const int MaximumJobLineNumber = 9999;

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
        if (coordinateSystem is < MinimumCoordinateSystem or > MaximumCoordinateSystem)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coordinateSystem),
                coordinateSystem,
                $"좌표계 번호는 {MinimumCoordinateSystem}에서 {MaximumCoordinateSystem} 사이여야 합니다.");
        }

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
    public Task<RobotResult<string>> ReadUserFrameAsync(int frameNumber, CancellationToken cancellationToken = default)
    {
        if (frameNumber is < MinimumUserFrame or > MaximumUserFrame)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameNumber),
                frameNumber,
                $"사용자 좌표계 번호는 {MinimumUserFrame}에서 {MaximumUserFrame} 사이여야 합니다.");
        }

        return ExecuteCommandAsync("RUFRAME", frameNumber.ToString(CultureInfo.InvariantCulture), cancellationToken);
    }

    /// <summary>컨트롤러 변수(<c>SAVEV</c>)를 조회합니다.</summary>
    public Task<RobotResult<string>> ReadVariableAsync(
        YrcVariableType variableType,
        string variableAddress,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(YrcVariableType), variableType))
        {
            throw new ArgumentOutOfRangeException(nameof(variableType), variableType, "정의되지 않은 YRC 변수 타입입니다.");
        }

        if (variableAddress is null)
        {
            throw new ArgumentNullException(nameof(variableAddress));
        }

        if (string.IsNullOrWhiteSpace(variableAddress))
        {
            throw new ArgumentException("변수 주소는 비어 있을 수 없습니다.", nameof(variableAddress));
        }

        YrcEthernetRequestBuilder.ValidateNoLineBreaks(variableAddress, nameof(variableAddress));
        return ExecuteCommandAsync("SAVEV", $"{((int)variableType).ToString(CultureInfo.InvariantCulture)},{variableAddress}", cancellationToken);
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
    public Task<RobotResult> SetModeAsync(int mode, CancellationToken cancellationToken = default)
    {
        if (mode is < MinimumMode or > MaximumMode)
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, $"운전 모드는 {MinimumMode}(교시) 또는 {MaximumMode}(재생)이어야 합니다.");
        }

        return ExecuteControlAsync("MODE", mode.ToString(CultureInfo.InvariantCulture), cancellationToken);
    }

    /// <summary>사이클 모드를 선택합니다. (1: Step, 2: 1-Cycle, 3: Auto)</summary>
    public Task<RobotResult> SetCycleAsync(int cycle, CancellationToken cancellationToken = default)
    {
        if (cycle is < MinimumCycle or > MaximumCycle)
        {
            throw new ArgumentOutOfRangeException(nameof(cycle), cycle, $"사이클 모드는 {MinimumCycle}(Step)에서 {MaximumCycle}(Auto) 사이여야 합니다.");
        }

        return ExecuteControlAsync("CYCLE", cycle.ToString(CultureInfo.InvariantCulture), cancellationToken);
    }

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
    public Task<RobotResult> StartJobAsync(string? programName = null, CancellationToken cancellationToken = default)
    {
        if (programName is not null)
        {
            YrcEthernetRequestBuilder.ValidateNoLineBreaks(programName, nameof(programName));
        }

        return ExecuteControlAsync("START", programName, cancellationToken);
    }

    /// <summary>지정 프로그램을 삭제합니다.</summary>
    public Task<RobotResult> DeleteJobAsync(string programName, CancellationToken cancellationToken = default)
    {
        ValidateProgramName(programName, nameof(programName));
        return ExecuteControlAsync("DELETE", programName, cancellationToken);
    }

    /// <summary>메인 JOB을 설정합니다.</summary>
    public Task<RobotResult> SetMasterJobAsync(string programName, CancellationToken cancellationToken = default)
    {
        ValidateProgramName(programName, nameof(programName));
        return ExecuteControlAsync("SETMJ", programName, cancellationToken);
    }

    /// <summary>실행 JOB 및 라인 번호를 설정합니다.</summary>
    public Task<RobotResult> SelectJobAsync(string programName, int lineNumber, CancellationToken cancellationToken = default)
    {
        ValidateProgramName(programName, nameof(programName));
        if (programName.IndexOf(',') >= 0)
        {
            throw new ArgumentException("프로그램 이름에 쉼표(',')를 포함할 수 없습니다.", nameof(programName));
        }

        if (lineNumber is < MinimumJobLineNumber or > MaximumJobLineNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lineNumber),
                lineNumber,
                $"JOB 라인 번호는 {MinimumJobLineNumber}에서 {MaximumJobLineNumber} 사이여야 합니다.");
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

    private static void ValidateProgramName(string programName, string parameterName)
    {
        if (programName is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (string.IsNullOrWhiteSpace(programName))
        {
            throw new ArgumentException("프로그램 이름은 비어 있을 수 없습니다.", parameterName);
        }

        YrcEthernetRequestBuilder.ValidateNoLineBreaks(programName, parameterName);
    }
}
