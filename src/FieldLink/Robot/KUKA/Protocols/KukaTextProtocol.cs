using FieldLink.PlcDrivers.Common;
using System;
using System.Text;

namespace FieldLink.Robot.KUKA.Protocols
{
    /// <summary>KUKA 사용자 정의 텍스트 명령을 구성합니다.</summary>
    public static partial class KukaTextProtocol
    {
        /// <summary>변수 이름을 쉼표로 연결한 00 읽기 명령을 구성합니다. null 배열은 빈 배열로 처리합니다.</summary>
        public static string BuildReadCommands(string[] address)
        {
            if (address == null)
                return string.Empty;
            StringBuilder sb = new StringBuilder("00");
            for (int i = 0; i < address.Length; i++)
            {
                sb.Append($"{address[i]}");
                if (i != address.Length - 1)
                    sb.Append(",");
            }

            return sb.ToString();
        }

        /// <summary>변수 이름을 쉼표로 연결한 00 읽기 명령을 구성합니다. null 배열은 빈 배열로 처리합니다.</summary>
        public static string BuildReadCommands(string address)
        {
            return BuildReadCommands(new string[] { address });
        }

        /// <summary>변수=값 목록을 쉼표로 연결한 01 쓰기 명령을 구성합니다. 배열 길이가 다르면 예외가 발생합니다.</summary>
        public static string BuildWriteCommands(string[] address, string[] values)
        {
            if (address == null || values == null)
                return string.Empty;
            if (address.Length != values.Length)
                throw new Exception(ProtocolMessages.TwoParametersLengthIsNotSame);
            StringBuilder sb = new StringBuilder("01");
            for (int i = 0; i < address.Length; i++)
            {
                sb.Append($"{address[i]}=");
                sb.Append($"{values[i]}");
                if (i != address.Length - 1)
                    sb.Append(",");
            }

            return sb.ToString();
        }

        /// <summary>변수=값 목록을 쉼표로 연결한 01 쓰기 명령을 구성합니다. 배열 길이가 다르면 예외가 발생합니다.</summary>
        public static string BuildWriteCommands(string address, string value)
        {
            return BuildWriteCommands(new string[] { address }, new string[] { value });
        }
    }
}
