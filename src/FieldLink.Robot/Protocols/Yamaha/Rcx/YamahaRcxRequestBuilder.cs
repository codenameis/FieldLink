using System;
using System.Globalization;
using System.Text;

namespace FieldLink.Robot.Protocols.Yamaha.Rcx;

/// <summary>YAMAHA RCX ASCII 온라인 명령 프레임을 생성하는 순수 정적 빌더입니다.</summary>
public static class YamahaRcxRequestBuilder
{
    private const string LineTerminator = "\r\n";
    private const string ResetCommand = "@ RESET ";
    private const string RunCommand = "@ RUN ";
    private const string StopCommand = "@ STOP ";
    private const string ReadMotorStatusCommand = "@?MOTOR ";
    private const string ReadModeStatusCommand = "@?MODE ";
    private const string ReadJointsCommand = "@?WHERE ";
    private const string ReadEmergencyStatusCommand = "@?EMG ";
    private const int DefaultRobotNumber = 1;
    private const int MinimumTaskId = 1;

    private static readonly char[] DisallowedControlCharacters = { '\r', '\n', '\0' };

    /// <summary>임의 온라인 명령 문자열에 <c>\r\n</c>을 붙여 ASCII 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildCommand(string command)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        string body = command.EndsWith(LineTerminator, StringComparison.Ordinal)
            ? command.Substring(0, command.Length - LineTerminator.Length)
            : command;

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("명령 문자열은 비어 있을 수 없습니다.", nameof(command));
        }

        if (body.IndexOfAny(DisallowedControlCharacters) >= 0)
        {
            throw new ArgumentException("명령 문자열에 개행(\\r, \\n) 또는 제어 문자를 포함할 수 없습니다.", nameof(command));
        }

        return Encoding.ASCII.GetBytes(body + LineTerminator);
    }

    /// <summary>프로그램 리셋(<c>@ RESET \r\n</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildReset() => BuildCommand(ResetCommand);

    /// <summary>프로그램 실행(<c>@ RUN \r\n</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildRun() => BuildCommand(RunCommand);

    /// <summary>프로그램 정지(<c>@ STOP \r\n</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildStop() => BuildCommand(StopCommand);

    /// <summary>프로그램 로드(<c>@ LOAD &lt;program&gt;, T{taskId}\r\n</c>) 요청 바이트를 생성합니다. (ASCII <c>@</c> 사용)</summary>
    public static byte[] BuildLoad(string program, int taskId)
    {
        if (program is null)
        {
            throw new ArgumentNullException(nameof(program));
        }

        if (string.IsNullOrWhiteSpace(program))
        {
            throw new ArgumentException("프로그램 이름은 비어 있을 수 없습니다.", nameof(program));
        }

        if (program.IndexOfAny(DisallowedControlCharacters) >= 0)
        {
            throw new ArgumentException("프로그램 이름에 개행 또는 제어 문자를 포함할 수 없습니다.", nameof(program));
        }

        if (taskId < MinimumTaskId)
        {
            throw new ArgumentOutOfRangeException(nameof(taskId), taskId, $"태스크 ID는 {MinimumTaskId} 이상이어야 합니다.");
        }

        return BuildCommand($"@ LOAD <{program}>, T{taskId.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>수동 직교 조그(<c>@ JOGXY ...</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildJogXY(int axis, int robot = DefaultRobotNumber)
    {
        if (axis == 0 || axis == int.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(axis), axis, "축 번호는 0 또는 최솟값일 수 없습니다.");
        }

        if (robot < DefaultRobotNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(robot), robot, $"로봇 번호는 {DefaultRobotNumber} 이상이어야 합니다.");
        }

        int absoluteAxis = Math.Abs(axis);
        char direction = axis > 0 ? '+' : '-';
        string text = robot == DefaultRobotNumber
            ? $"@ JOGXY {absoluteAxis.ToString(CultureInfo.InvariantCulture)}{direction}"
            : $"@ JOGXY [{robot.ToString(CultureInfo.InvariantCulture)}] {absoluteAxis.ToString(CultureInfo.InvariantCulture)}{direction}";

        return BuildCommand(text);
    }

    /// <summary>모터 전원 상태 조회(<c>@?MOTOR \r\n</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildReadMotorStatus() => BuildCommand(ReadMotorStatusCommand);

    /// <summary>운전 모드 상태 조회(<c>@?MODE \r\n</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildReadModeStatus() => BuildCommand(ReadModeStatusCommand);

    /// <summary>현재 관절 좌표 조회(<c>@?WHERE \r\n</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildReadJoints() => BuildCommand(ReadJointsCommand);

    /// <summary>비상정지 상태 조회(<c>@?EMG \r\n</c>) 요청 바이트를 생성합니다.</summary>
    public static byte[] BuildReadEmergencyStatus() => BuildCommand(ReadEmergencyStatusCommand);
}
