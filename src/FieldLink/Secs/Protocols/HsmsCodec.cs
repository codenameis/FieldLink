using System;
using System.IO;
using FieldLink.Communication.Framing;
using FieldLink.Secs.Types;

namespace FieldLink.Secs.Protocols
{
    /// <summary>4바이트 길이와 10바이트 HSMS 헤더를 생성·검증합니다.</summary>
    public static class HsmsCodec
    {
        /// <summary>길이 헤더를 포함한 프레임 경계를 생성합니다.</summary>
        public static IFrameBoundary CreateFrameBoundary(int maximumFrameLength = 1024 * 1024)
        {
            if (maximumFrameLength < 14)
                throw new ArgumentOutOfRangeException(nameof(maximumFrameLength));
            return new HeaderLengthFrame(4, segment => FrameLength(segment.Array, segment.Offset, maximumFrameLength));
        }
        /// <summary>HSMS 프레임을 생성합니다.</summary>
        public static byte[] Encode(SecsMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            var bytes = new byte[checked(14 + message.Body.Length)];
            Write32(bytes, 0, (uint)(bytes.Length - 4));
            bytes[4] = (byte)(message.DeviceID >> 8);
            bytes[5] = (byte)message.DeviceID;
            bytes[6] = (byte)(message.StreamNo | (message.W ? 128 : 0));
            bytes[7] = message.FunctionNo;
            bytes[8] = message.PresentationType;
            bytes[9] = message.SessionType;
            Write32(bytes, 10, message.MessageID);
            Buffer.BlockCopy(message.Body, 0, bytes, 14, message.Body.Length);
            return bytes;
        }
        /// <summary>한 프레임을 해석합니다. 프레임 앞뒤에 추가 데이터가 있으면 거부합니다.</summary>
        public static SecsMessage Decode(byte[] frame, int maximumFrameLength = 1024 * 1024)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (maximumFrameLength < 14)
                throw new ArgumentOutOfRangeException(nameof(maximumFrameLength));
            if (frame.Length < 14 || FrameLength(frame, 0, maximumFrameLength) != frame.Length)
                throw new InvalidDataException("Invalid HSMS frame length.");
            byte[] body = new byte[frame.Length - 14];
            Buffer.BlockCopy(frame, 14, body, 0, body.Length);
            return new SecsMessage((ushort)(frame[4] * 256 + frame[5]), (byte)(frame[6] & 127), frame[7],
                Read32(frame, 10), body, (frame[6] & 128) != 0, frame[9], frame[8]);
        }
        private static int FrameLength(byte[] bytes, int offset, int maximum)
        {
            uint length = Read32(bytes, offset);
            if (length < 10 || length > (uint)(maximum - 4))
                throw new InvalidDataException("HSMS length is outside the permitted range.");
            return (int)length + 4;
        }
        internal static uint Read32(byte[] bytes, int offset) =>
            ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
        internal static void Write32(byte[] bytes, int offset, uint value)
        {
            for (int i = 3; i >= 0; i--)
            {
                bytes[offset + i] = (byte)value;
                value >>= 8;
            }
        }
    }
}
