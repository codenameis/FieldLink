using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FieldLink.Secs.Types;

namespace FieldLink.Secs.Protocols
{
    /// <summary>SECS-II 형식 코드, 1~3바이트 길이와 big-endian 숫자를 처리합니다.</summary>
    public static class Secs2Codec
    {
        /// <summary>값을 직렬화합니다. 단일 항목 길이는 0xFFFFFF, 중첩 깊이는 64로 제한합니다.</summary>
        public static byte[] Encode(SecsValue value, Encoding encoding)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            var bytes = new List<byte>();
            Write(value, encoding, bytes, 0);
            return bytes.ToArray();
        }
        /// <summary>전체 본문을 검증하고 해석합니다. 빈 본문은 None입니다.</summary>
        public static SecsValue Decode(byte[] data, Encoding encoding)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            if (data.Length == 0)
                return new SecsValue();
            int offset = 0;
            SecsValue value = Read(data, ref offset, encoding, 0);
            if (offset != data.Length)
                throw new InvalidDataException("Trailing bytes after SECS item.");
            return value;
        }
        private static void Write(SecsValue value, Encoding encoding, List<byte> output, int depth)
        {
            CheckDepth(depth);
            if (value.ItemType == SecsItemType.None)
                return;
            byte[] payload = null;
            int length;
            if (value.ItemType == SecsItemType.List)
                length = value.Length;
            else
            {
                payload = Payload(value, encoding);
                length = payload.Length;
            }
            if (length > 0xffffff)
                throw new ArgumentOutOfRangeException(nameof(value), "SECS item exceeds its 24-bit length.");
            int count = length < 256 ? 1 : length < 65536 ? 2 : 3;
            output.Add((byte)(Code(value.ItemType) | count));
            for (int i = count - 1; i >= 0; i--)
                output.Add((byte)(length >> (8 * i)));
            if (payload != null)
                output.AddRange(payload);
            else
                foreach (var child in SecsValue.RequireList(value))
                {
                    if (child == null || child.ItemType == SecsItemType.None)
                        throw new ArgumentException("A list cannot contain absent items.");
                    Write(child, encoding, output, depth + 1);
                }
        }
        private static byte[] Payload(SecsValue value, Encoding encoding)
        {
            if (value.ItemType == SecsItemType.ASCII)
                return encoding.GetBytes((string)value.Value);
            Array values = value.Value as Array;
            if (values == null)
            {
                values = Array.CreateInstance(SecsValue.ElementType(value.ItemType), 1);
                values.SetValue(value.Value, 0);
            }
            int width = Width(value.ItemType);
            var data = new byte[checked(values.Length * width)];
            if (value.ItemType == SecsItemType.Bool)
            {
                for (int i = 0; i < values.Length; i++)
                    data[i] = (bool)values.GetValue(i) ? (byte)255 : (byte)0;
            }
            else
                Buffer.BlockCopy(values, 0, data, 0, data.Length);
            ReverseWords(data, width);
            return data;
        }
        private static SecsValue Read(byte[] data, ref int offset, Encoding encoding, int depth)
        {
            CheckDepth(depth);
            Require(data, offset, 1);
            byte header = data[offset++];
            int count = header & 3;
            if (count == 0)
                throw new InvalidDataException("SECS length requires 1 to 3 bytes.");
            Require(data, offset, count);
            int length = 0;
            for (int i = 0; i < count; i++)
                length = (length << 8) | data[offset++];
            SecsItemType type = TypeFromCode(header & 0xfc);
            if (type == SecsItemType.List)
            {
                // Every child requires at least a format and length byte.
                if (length > (data.Length - offset) / 2)
                    throw new InvalidDataException("List length exceeds available items.");
                var children = new SecsValue[length];
                for (int i = 0; i < length; i++)
                    children[i] = Read(data, ref offset, encoding, depth + 1);
                return new SecsValue(type, children);
            }
            Require(data, offset, length);
            int start = offset;
            offset += length;
            if (type == SecsItemType.ASCII)
                return new SecsValue(encoding.GetString(data, start, length));
            int width = Width(type);
            if (length % width != 0)
                throw new InvalidDataException("Numeric SECS length is not a multiple of element width.");
            byte[] payload = new byte[length];
            Buffer.BlockCopy(data, start, payload, 0, length);
            ReverseWords(payload, width);
            Array values = Array.CreateInstance(SecsValue.ElementType(type), length / width);
            if (type == SecsItemType.Bool)
            {
                for (int i = 0; i < length; i++)
                    values.SetValue(payload[i] != 0, i);
            }
            else
                Buffer.BlockCopy(payload, 0, values, 0, length);
            return new SecsValue(type, values.Length == 1 && type != SecsItemType.Binary && type != SecsItemType.JIS8 ?
                values.GetValue(0) : values);
        }
        private static void Require(byte[] data, int offset, int length)
        {
            if (length > data.Length - offset)
                throw new InvalidDataException("Truncated SECS item.");
        }
        private static void CheckDepth(int depth)
        {
            if (depth > 64)
                throw new InvalidDataException("SECS nesting exceeds 64 levels.");
        }
        private static void ReverseWords(byte[] data, int width)
        {
            if (BitConverter.IsLittleEndian && width > 1)
                for (int i = 0; i < data.Length; i += width)
                    Array.Reverse(data, i, width);
        }
        private static int Width(SecsItemType type)
        {
            switch (type)
            {
                case SecsItemType.Int16:
                case SecsItemType.UInt16: return 2;
                case SecsItemType.Int32:
                case SecsItemType.UInt32:
                case SecsItemType.Single: return 4;
                case SecsItemType.Int64:
                case SecsItemType.UInt64:
                case SecsItemType.Double: return 8;
                default: return 1;
            }
        }
        private static int Code(SecsItemType type)
        {
            switch (type)
            {
                case SecsItemType.List: return 0;
                case SecsItemType.Binary: return 0x20;
                case SecsItemType.ASCII: return 0x40;
                case SecsItemType.JIS8: return 0x44;
                case SecsItemType.Bool: return 36;
                case SecsItemType.SByte: return 100;
                case SecsItemType.Byte: return 164;
                case SecsItemType.Int16: return 104;
                case SecsItemType.UInt16: return 168;
                case SecsItemType.Int32: return 112;
                case SecsItemType.UInt32: return 176;
                case SecsItemType.Int64: return 96;
                case SecsItemType.UInt64: return 160;
                case SecsItemType.Single: return 144;
                case SecsItemType.Double: return 128;
                default: throw new InvalidDataException("Unknown SECS format.");
            }
        }
        private static SecsItemType TypeFromCode(int code)
        {
            foreach (SecsItemType type in Enum.GetValues(typeof(SecsItemType)))
                if (type != SecsItemType.None && Code(type) == code)
                    return type;
            throw new InvalidDataException("Unknown SECS format.");
        }
    }
}
