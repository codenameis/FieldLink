using System;
using System.Text;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>YRC TCP의 연결 협상, 두 단계 요청 및 응답 종료 규칙입니다.</summary>
    public static class YrcTcpProtocol
    {
        /// <summary>연결마다 먼저 전송할 KeepAlive 협상 명령입니다.</summary>
        public static byte[] BuildConnect() => Encoding.ASCII.GetBytes("CONNECT Robot_access KeepAlive:-1\r\n");
        /// <summary>성공 응답에서 지속 연결 지원 여부를 구합니다. 재접속 시 반드시 다시 협상해야 합니다.</summary>
        public static OperationResult<bool> ParseConnect(string response)
        {
            if (response == "OK:YR Information Server(Ver) Keep-Alive:-1.\r\n")
                return OperationResult.CreateSuccessResult(true);
            if (response.StartsWith("OK:"))
                return OperationResult.CreateSuccessResult(false);
            return new OperationResult<bool>(response);
        }

        /// <summary>명령 헤더를 구성합니다. 원본처럼 본문의 문자 수에 CR 한 글자를 더한 길이를 사용합니다.</summary>
        public static byte[] BuildHeader(string command, string data) => Encoding.Default.GetBytes($"HOSTCTRL_REQUEST {command} {(string.IsNullOrEmpty(data) ? 0 : data.Length + 1)}\r\n");
        /// <summary>헤더 승인 뒤에 보낼 ASCII 본문입니다. 빈 본문에는 아무것도 전송하지 않습니다.</summary>
        public static byte[] BuildBody(string data) => string.IsNullOrEmpty(data) ? new byte[0] : Encoding.ASCII.GetBytes(data + "\r");
        /// <summary>CRLF까지 수신한 헤더 승인 응답을 검사합니다.</summary>
        public static OperationResult CheckHeaderResponse(string response) => response.StartsWith("OK:") ? OperationResult.CreateSuccessResult() : new OperationResult(response.Remove(response.Length - 2));
        /// <summary>CR까지 수신한 본문 뒤에 LF 한 바이트를 추가로 소비해야 하는지 판정합니다.</summary>
        public static bool RequiresTrailingLf(string response) => response.StartsWith("ERROR:") || response.StartsWith("0000\r");
        /// <summary>CR까지 수신한 본문을 해석합니다. 필요한 LF 소비는 호출자가 별도로 수행합니다.</summary>
        public static OperationResult<string> ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return new OperationResult<string>("응답이 비어 있습니다.");
            if (response.StartsWith("ERROR:"))
                return YrcErrorParser.ExtraErrorMessage(response);
            if (response.StartsWith("0000\r"))
                return OperationResult.CreateSuccessResult("0000");
            return OperationResult.CreateSuccessResult(response.Remove(response.Length - 1));
        }
    }
}
