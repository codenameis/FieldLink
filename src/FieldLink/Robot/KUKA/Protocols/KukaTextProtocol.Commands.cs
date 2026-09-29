using System.Text;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.KUKA.Protocols
{
    /// <summary>KUKA 사용자 정의 텍스트 명령과 응답을 변환합니다.</summary>
    public static partial class KukaTextProtocol
    {
        /// <summary>사용자 정의 텍스트 명령을 UTF-8 바이트로 변환합니다. 종결 문자는 붙이지 않습니다.</summary>
        public static byte[] Encode(string command) => Encoding.UTF8.GetBytes(command);
        /// <summary>프로그램 시작 명령을 구성합니다.</summary>
        public static string BuildStartProgram(string program) => "03" + program;
        /// <summary>프로그램 초기화 명령을 구성합니다.</summary>
        public static string BuildResetProgram() => "0601";
        /// <summary>프로그램 정지 명령을 구성합니다.</summary>
        public static string BuildStopProgram() => "0621";
        /// <summary>원본과 동일하게 소문자 err 포함 여부로 명령 응답을 판정합니다.</summary>
        public static OperationResult ParseCommandResponse(byte[] response)
        {
            string message = Encoding.UTF8.GetString(response);
            return message.Contains("err") ? new OperationResult("응답에 err이 포함되어 있습니다: " + message) : OperationResult.CreateSuccessResult();
        }

        /// <summary>일반 읽기 응답을 변형하지 않고 반환합니다.</summary>
        public static OperationResult<byte[]> ParseReadResponse(byte[] response) => OperationResult.CreateSuccessResult(response);
    }
}
