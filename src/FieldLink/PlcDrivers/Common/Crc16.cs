using System;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>하위 비트부터 계산하는 16비트 CRC입니다. 기본 설정은 Modbus RTU입니다.</summary>
    /// <remarks>Modbus Serial Line V1.02 §6.2.2. polynomial은 반사된 다항식 표현입니다.</remarks>
    public static class Crc16
    {
        /// <summary>입력 구간의 CRC를 계산합니다. 입력을 수정하거나 보관하지 않습니다.</summary>
        public static ushort Compute(ArraySegment<byte> data, ushort polynomial = 0xA001, ushort initialValue = 0xFFFF)
        {
            if (data.Array == null)
                throw new ArgumentException("입력 배열이 필요합니다.", nameof(data));
            ushort remainder = initialValue;
            for (int index = data.Offset; index < data.Offset + data.Count; index++)
            {
                remainder ^= data.Array[index];
                for (int bit = 0; bit < 8; bit++)
                    remainder = (ushort)((remainder >> 1) ^ ((remainder & 1) != 0 ? polynomial : 0));
            }
            return remainder;
        }

        /// <summary>입력의 복사본 뒤에 CRC의 하위 바이트와 상위 바이트를 차례로 붙입니다.</summary>
        public static byte[] Append(byte[] data, ushort polynomial = 0xA001, ushort initialValue = 0xFFFF)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            ushort checksum = Compute(new ArraySegment<byte>(data), polynomial, initialValue);
            byte[] frame = new byte[checked(data.Length + 2)];
            Buffer.BlockCopy(data, 0, frame, 0, data.Length);
            frame[data.Length] = (byte)checksum;
            frame[data.Length + 1] = (byte)(checksum >> 8);
            return frame;
        }

        /// <summary>끝의 CRC 두 바이트를 검사합니다. null 또는 두 바이트 미만이면 false입니다.</summary>
        public static bool Verify(byte[] frame, ushort polynomial = 0xA001, ushort initialValue = 0xFFFF)
        {
            if (frame == null || frame.Length < 2)
                return false;
            int length = frame.Length - 2;
            ushort checksum = Compute(new ArraySegment<byte>(frame, 0, length), polynomial, initialValue);
            return frame[length] == (byte)checksum && frame[length + 1] == (byte)(checksum >> 8);
        }
    }
}
