using FieldLink.PlcDrivers.Common;
using System;
using System.Text;
using System.Collections.Generic;

namespace FieldLink.Robot.KUKA.Protocols
{
    /// <summary>KUKAVARPROXY의 명령 프레임과 응답 본문을 변환합니다.</summary>
    public static class KukaVarProxyProtocol
    {
        private static readonly IProtocolValueConverter ValueConverter = new ProtocolValueConverter(ByteOrder.LittleEndianWithByteSwap);
        /// <summary>메시지 ID와 본문 길이를 빅 엔디언으로 붙여 전체 프레임을 구성합니다.</summary>
        public static byte[] PackCommand(byte[] commandCore, ushort messageId)
        {
            byte[] buffer = new byte[commandCore.Length + 4];
            ValueConverter.GetBytes(messageId).CopyTo(buffer, 0);
            ValueConverter.GetBytes((ushort)commandCore.Length).CopyTo(buffer, 2);
            commandCore.CopyTo(buffer, 4);
            return buffer;
        }

        /// <summary>선언 길이와 3바이트 상태 tail을 검사하고 응답의 값 영역을 추출합니다.</summary>
        public static OperationResult<byte[]> ExtractActualData(byte[] response)
        {
            if (response == null || response.Length < 10)
                return new OperationResult<byte[]>("KUKAVARPROXY 응답 헤더 또는 상태 tail이 부족합니다.");
            int bodyLength = response[2] * 256 + response[3];
            int length = response[5] * 256 + response[6];
            if (bodyLength != response.Length - 4 || length != response.Length - 10)
                return new OperationResult<byte[]>("KUKAVARPROXY 선언 길이가 실제 응답과 다릅니다.");
            try
            {
                if (response[response.Length - 1] != 0x01)
                    return new OperationResult<byte[]>(response[response.Length - 1], "Wrong: " + ProtocolBytes.ByteToHexString(response, ' '));
                if (response[response.Length - 3] != 0 || response[response.Length - 2] != 1)
                    return new OperationResult<byte[]>("KUKAVARPROXY 성공 상태 tail이 올바르지 않습니다.");
                byte[] buffer = new byte[length];
                Array.Copy(response, 7, buffer, 0, length);
                return OperationResult.CreateSuccessResult(buffer);
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>("Wrong:" + ex.Message + " Code:" + ProtocolBytes.ByteToHexString(response, ' '));
            }
        }

        /// <summary>기능 번호와 길이가 붙은 변수 문자열을 구성합니다.</summary>
        public static byte[] BuildCommands(byte function, string[] commands)
        {
            List<byte> buffer = new List<byte>();
            buffer.Add(function);
            for (int i = 0; i < commands.Length; i++)
            {
                byte[] buffer_command = Encoding.Default.GetBytes(commands[i]);
                buffer.AddRange(ValueConverter.GetBytes((ushort)buffer_command.Length));
                buffer.AddRange(buffer_command);
            }

            return buffer.ToArray();
        }

        /// <summary>변수 읽기 본문을 구성합니다.</summary>
        public static byte[] BuildReadValueCommand(string address) => BuildCommands(0x00, new string[] { address });
        /// <summary>변수 쓰기 본문을 구성합니다.</summary>
        public static byte[] BuildWriteValueCommand(string address, string value) => BuildCommands(0x01, new string[] { address, value });
    }
}
