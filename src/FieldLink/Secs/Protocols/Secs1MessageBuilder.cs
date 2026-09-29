using System;
using System.Collections.Generic;

namespace FieldLink.Secs.Protocols
{
    /// <summary>참고 구현의 SECS-I 블록 생성 규칙을 보존합니다. Serial 통신 절차는 제공하지 않습니다.</summary>
    public static class Secs1MessageBuilder
    {
        /// <summary>244바이트 이하 단일 블록, 초과 시 224바이트 분할과 16비트 합계를 적용합니다.</summary>
        /// <remarks>참고 구현과 동일하게 모든 분할 블록에 입력 blockNumber를 사용합니다. 완성된 SECS-I 전송기가 아닙니다.</remarks>
        public static IReadOnlyList<byte[]> Build(ushort deviceID, byte stream, byte function, ushort blockNumber,
            uint messageID, byte[] data, bool replyExpected)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (stream > 127)
                throw new ArgumentOutOfRangeException(nameof(stream));
            if (blockNumber > 32767)
                throw new ArgumentOutOfRangeException(nameof(blockNumber));
            int size = data.Length <= 244 ? 244 : 224;
            var result = new List<byte[]>();
            for (int offset = 0; offset < data.Length; offset += size)
            {
                int count = Math.Min(size, data.Length - offset);
                byte[] block = new byte[13 + count];
                block[0] = (byte)(10 + count);
                block[1] = (byte)(deviceID >> 8);
                block[2] = (byte)deviceID;
                block[3] = (byte)(stream | (replyExpected ? 128 : 0));
                block[4] = function;
                block[5] = (byte)((blockNumber >> 8) | (offset + count == data.Length ? 128 : 0));
                block[6] = (byte)blockNumber;
                HsmsCodec.Write32(block, 7, messageID);
                Buffer.BlockCopy(data, offset, block, 11, count);
                int sum = 0;
                for (int i = 1; i < block.Length - 2; i++)
                    sum += block[i];
                block[block.Length - 2] = (byte)(sum >> 8);
                block[block.Length - 1] = (byte)sum;
                result.Add(block);
            }
            return result;
        }
    }
}
