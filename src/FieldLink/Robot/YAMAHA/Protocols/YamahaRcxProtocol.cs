using System;
using System.Linq;
using System.Text;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.YAMAHA.Protocols
{
    /// <summary>RCX의 ASCII 명령과 CRLF로 끝나는 응답 행을 변환합니다.</summary>
    public static class YamahaRcxProtocol
    {
        /// <summary>명령 뒤에 CRLF를 붙입니다. 응답 수신과 행 개수 관리는 호출자의 책임입니다.</summary>
        public static byte[] BuildCommand(string command) => Encoding.ASCII.GetBytes(command + "\r\n");
        /// <summary>첫 행에는 명령을 전송하고 이후 행은 추가 전송 없이 수신합니다. 빈 배열은 수신 전용 단계입니다.</summary>
        public static byte[][] BuildExchangeSequence(byte[] command, int lines)
        {
            var steps = new byte[Math.Max(0, lines)][];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = i == 0 ? (byte[])command.Clone() : new byte[0];
            return steps;
        }

        /// <summary>초기화 명령입니다. 응답은 1행입니다.</summary>
        public static byte[] BuildReset() => BuildCommand("@ RESET ");
        /// <summary>실행 명령입니다. 응답은 1행입니다.</summary>
        public static byte[] BuildRun() => BuildCommand("@ RUN ");
        /// <summary>정지 명령입니다. 응답은 1행입니다.</summary>
        public static byte[] BuildStop() => BuildCommand("@ STOP ");
        /// <summary>ASCII @로 시작하는 프로그램 로드 명령입니다. 응답은 1행입니다.</summary>
        public static byte[] BuildLoad(string program, int taskId) => BuildCommand($"@ LOAD <{program}>, T{taskId}");
        /// <summary>축 조그 명령입니다. 축 부호가 방향을 나타내며 응답은 2행입니다.</summary>
        public static byte[] BuildJogXY(int axis, int robot = 1) => BuildCommand("@ JOGXY " + (robot == 1 ? "" : $"[{robot}] ") + Math.Abs(axis) + (axis > 0 ? "+" : "-"));
        /// <summary>모터 상태 조회입니다. 값과 OK를 2행으로 수신합니다.</summary>
        public static byte[] BuildReadMotorStatus() => BuildCommand("@?MOTOR ");
        /// <summary>모드 조회입니다. 값과 OK를 2행으로 수신합니다.</summary>
        public static byte[] BuildReadModeStatus() => BuildCommand("@?MODE ");
        /// <summary>관절 좌표 조회입니다. 응답은 1행입니다.</summary>
        public static byte[] BuildReadJoints() => BuildCommand("@?WHERE ");
        /// <summary>비상 상태 조회입니다. 값과 OK를 2행으로 수신합니다.</summary>
        public static byte[] BuildReadEmergencyStatus() => BuildCommand("@?EMG ");
        /// <summary>완전한 응답 행에서 마지막 CRLF 두 바이트를 제거합니다.</summary>
        public static string ParseLine(byte[] line) => Encoding.ASCII.GetString(line.RemoveLast(2));
        /// <summary>원본과 동일하게 OK로 시작하는 응답을 성공으로 판정합니다.</summary>
        public static OperationResult CheckResponseOk(string line) => line.StartsWith("OK") ? OperationResult.CreateSuccessResult() : new OperationResult(line);
        /// <summary>두 번째 행의 성공 여부를 확인하고 첫 번째 행의 정수를 해석합니다.</summary>
        public static OperationResult<int> ParseStatus(string[] lines)
        {
            var result = CheckResponseOk(lines[1]);
            return result.IsSuccess ? OperationResult.CreateSuccessResult(Convert.ToInt32(lines[0])) : OperationResult.CreateFailedResult<int>(result);
        }

        /// <summary>첫 번째 행에서 공백으로 구분한 관절 좌표를 해석합니다. 원본의 현재 문화권 변환을 유지합니다.</summary>
        public static float[] ParseJoints(string line) => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(Convert.ToSingle).ToArray();
    }
}
