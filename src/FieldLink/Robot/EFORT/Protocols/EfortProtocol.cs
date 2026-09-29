using System;
using System.Text;

namespace FieldLink.Robot.EFORT.Protocols
{
    /// <summary>ER7BC10 상태 조회 프레임을 구성합니다. 하트비트 번호는 호출자가 관리합니다.</summary>
    public static class EfortProtocol
    {
        /// <summary>현재 형식의 38바이트 조회 명령을 구성합니다.</summary>
        public static byte[] BuildReadCommand(ushort heartbeat) => Build(heartbeat, 16);
        /// <summary>이전 형식의 36바이트 조회 명령을 구성합니다.</summary>
        public static byte[] BuildPreviousReadCommand(ushort heartbeat) => Build(heartbeat, 15);
        private static byte[] Build(ushort heartbeat, int markerLength)
        {
            byte[] command = new byte[markerLength * 2 + 6];
            Encoding.ASCII.GetBytes("MessageHead").CopyTo(command, 0);
            BitConverter.GetBytes((ushort)command.Length).CopyTo(command, markerLength);
            BitConverter.GetBytes((ushort)1001).CopyTo(command, markerLength + 2);
            BitConverter.GetBytes(heartbeat).CopyTo(command, markerLength + 4);
            Encoding.ASCII.GetBytes("MessageTail").CopyTo(command, markerLength + 6);
            return command;
        }
    }
}
