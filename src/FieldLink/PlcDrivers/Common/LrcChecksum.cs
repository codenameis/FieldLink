using System;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>바이트 합의 2의 보수로 계산하는 Modbus ASCII LRC입니다.</summary>
    /// <remarks>Modbus Serial Line V1.02 §6.2.1. ASCII 인코딩 전의 바이트를 입력합니다.</remarks>
    public static class LrcChecksum
    {
        /// <summary>입력 구간의 LRC를 계산합니다. 입력을 수정하거나 보관하지 않습니다.</summary>
        public static byte Compute(ArraySegment<byte> data) => unchecked((byte)-AdditiveChecksum.Compute(data));

        /// <summary>입력을 복사한 뒤 LRC 한 바이트를 붙입니다.</summary>
        public static byte[] Append(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            byte[] frame = new byte[checked(data.Length + 1)];
            Buffer.BlockCopy(data, 0, frame, 0, data.Length);
            frame[data.Length] = Compute(new ArraySegment<byte>(data));
            return frame;
        }

        /// <summary>끝의 LRC를 검사합니다. null 또는 빈 입력이면 false입니다.</summary>
        public static bool Verify(byte[] frame) => frame != null && frame.Length > 0 &&
            AdditiveChecksum.Compute(new ArraySegment<byte>(frame)) == 0;
    }
}
