using System;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>바이트 합의 하위 8비트와 대문자 ASCII 16진수 체크섬을 처리합니다.</summary>
    public static class AdditiveChecksum
    {
        private const string HexDigits = "0123456789ABCDEF";

        /// <summary>입력 구간을 더하고 상위 비트를 버립니다.</summary>
        public static byte Compute(ArraySegment<byte> data)
        {
            if (data.Array == null)
                throw new ArgumentException("입력 배열이 필요합니다.", nameof(data));
            byte sum = 0;
            for (int index = data.Offset; index < data.Offset + data.Count; index++)
                sum = unchecked((byte)(sum + data.Array[index]));
            return sum;
        }

        /// <summary>시작 위치부터 체크섬 직전까지 계산해 지정 위치의 두 바이트에 기록합니다.</summary>
        public static void WriteAscii(byte[] buffer, int offset, int checksumOffset)
        {
            Validate(buffer, offset, checksumOffset);
            byte sum = Compute(new ArraySegment<byte>(buffer, offset, checksumOffset - offset));
            buffer[checksumOffset] = (byte)HexDigits[sum >> 4];
            buffer[checksumOffset + 1] = (byte)HexDigits[sum & 15];
        }

        /// <summary>대문자 ASCII 16진수 두 바이트를 검사합니다. 입력을 수정하지 않습니다.</summary>
        public static bool VerifyAscii(byte[] buffer, int offset, int checksumOffset)
        {
            Validate(buffer, offset, checksumOffset);
            byte sum = Compute(new ArraySegment<byte>(buffer, offset, checksumOffset - offset));
            return buffer[checksumOffset] == (byte)HexDigits[sum >> 4] &&
                buffer[checksumOffset + 1] == (byte)HexDigits[sum & 15];
        }

        private static void Validate(byte[] buffer, int offset, int checksumOffset)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (checksumOffset < offset || checksumOffset > buffer.Length - 2)
                throw new ArgumentOutOfRangeException(nameof(checksumOffset));
        }
    }
}
