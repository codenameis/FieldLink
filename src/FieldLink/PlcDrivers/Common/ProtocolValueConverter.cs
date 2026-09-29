using System;
using System.Text;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>변경 불가능한 바이트 순서 설정으로 프로토콜 값과 바이트 배열을 변환합니다.</summary>
    /// <remarks>입력 버퍼를 보관·수정하지 않으며 반환 배열은 호출자 소유입니다.
    /// 배열 읽기의 length는 원소 수, 문자열 length는 바이트 수입니다.
    /// 문자열 워드 교환은 홀수 입력에 0을 덧붙인 뒤 수행합니다.</remarks>
    public sealed class ProtocolValueConverter : IProtocolValueConverter
    {
        /// <summary>바이트 순서와 문자열 워드 내 바이트 교환을 지정합니다.</summary>
        public ProtocolValueConverter(ByteOrder byteOrder = ByteOrder.LittleEndian, bool swapStringBytes = false)
        {
            if (byteOrder < ByteOrder.BigEndian || byteOrder > ByteOrder.LittleEndian)
                throw new ArgumentOutOfRangeException(nameof(byteOrder));
            ByteOrder = byteOrder;
            SwapStringBytes = swapStringBytes;
        }

        /// <inheritdoc />
        public ByteOrder ByteOrder { get; }
        /// <inheritdoc />
        public bool SwapStringBytes { get; }
        /// <inheritdoc />
        public IProtocolValueConverter WithByteOrder(ByteOrder byteOrder) => new ProtocolValueConverter(byteOrder, SwapStringBytes);

        /// <inheritdoc />
        public byte ReadByte(byte[] buffer, int index)
        {
            ValidateRange(buffer, index, 1, 1);
            return buffer[index];
        }

        /// <inheritdoc />
        public byte[] ReadBytes(byte[] buffer, int index, int length)
        {
            ValidateRange(buffer, index, length, 1);
            byte[] result = new byte[length];
            Array.Copy(buffer, index, result, 0, length);
            return result;
        }

        /// <inheritdoc />
        public bool ReadBoolean(byte[] buffer, int index)
        {
            ValidateBits(buffer, index, 1);
            return (buffer[index / 8] & (1 << (index % 8))) != 0;
        }

        /// <inheritdoc />
        public bool[] ReadBoolean(byte[] buffer, int index, int length)
        {
            ValidateBits(buffer, index, length);
            bool[] result = new bool[length];
            for (int i = 0; i < length; i++)
                result[i] = ReadBoolean(buffer, index + i);
            return result;
        }

        /// <inheritdoc />
        public byte[] GetBytes(bool value) => new byte[] { value ? (byte)1 : (byte)0 };
        /// <inheritdoc />
        public byte[] GetBytes(byte value) => new byte[] { value };
        /// <inheritdoc />
        public byte[] GetBytes(bool[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            byte[] result = new byte[values.Length / 8 + (values.Length % 8 == 0 ? 0 : 1)];
            for (int i = 0; i < values.Length; i++)
                if (values[i])
                    result[i / 8] |= (byte)(1 << (i % 8));
            return result;
        }

        /// <inheritdoc />
        public short ReadInt16(byte[] buffer, int index) => unchecked((short)ReadUnsigned(buffer, index, 2));
        /// <inheritdoc />
        public short[] ReadInt16(byte[] buffer, int index, int length) => ReadArray(buffer, index, length, 2, ReadInt16);
        /// <inheritdoc />
        public short[,] ReadInt16(byte[] buffer, int index, int row, int col) => ReadMatrix(buffer, index, row, col, 2, ReadInt16);
        /// <inheritdoc />
        public byte[] GetBytes(short value) => EncodeUnsigned(unchecked((ulong)value), 2);
        /// <inheritdoc />
        public byte[] GetBytes(short[] values) => EncodeArray(values, 2, value => unchecked((ulong)value));

        /// <inheritdoc />
        public ushort ReadUInt16(byte[] buffer, int index) => unchecked((ushort)ReadUnsigned(buffer, index, 2));
        /// <inheritdoc />
        public ushort[] ReadUInt16(byte[] buffer, int index, int length) => ReadArray(buffer, index, length, 2, ReadUInt16);
        /// <inheritdoc />
        public ushort[,] ReadUInt16(byte[] buffer, int index, int row, int col) => ReadMatrix(buffer, index, row, col, 2, ReadUInt16);
        /// <inheritdoc />
        public byte[] GetBytes(ushort value) => EncodeUnsigned(unchecked((ulong)value), 2);
        /// <inheritdoc />
        public byte[] GetBytes(ushort[] values) => EncodeArray(values, 2, value => unchecked((ulong)value));

        /// <inheritdoc />
        public int ReadInt32(byte[] buffer, int index) => unchecked((int)ReadUnsigned(buffer, index, 4));
        /// <inheritdoc />
        public int[] ReadInt32(byte[] buffer, int index, int length) => ReadArray(buffer, index, length, 4, ReadInt32);
        /// <inheritdoc />
        public int[,] ReadInt32(byte[] buffer, int index, int row, int col) => ReadMatrix(buffer, index, row, col, 4, ReadInt32);
        /// <inheritdoc />
        public byte[] GetBytes(int value) => EncodeUnsigned(unchecked((ulong)value), 4);
        /// <inheritdoc />
        public byte[] GetBytes(int[] values) => EncodeArray(values, 4, value => unchecked((ulong)value));

        /// <inheritdoc />
        public uint ReadUInt32(byte[] buffer, int index) => unchecked((uint)ReadUnsigned(buffer, index, 4));
        /// <inheritdoc />
        public uint[] ReadUInt32(byte[] buffer, int index, int length) => ReadArray(buffer, index, length, 4, ReadUInt32);
        /// <inheritdoc />
        public uint[,] ReadUInt32(byte[] buffer, int index, int row, int col) => ReadMatrix(buffer, index, row, col, 4, ReadUInt32);
        /// <inheritdoc />
        public byte[] GetBytes(uint value) => EncodeUnsigned(unchecked((ulong)value), 4);
        /// <inheritdoc />
        public byte[] GetBytes(uint[] values) => EncodeArray(values, 4, value => unchecked((ulong)value));

        /// <inheritdoc />
        public long ReadInt64(byte[] buffer, int index) => unchecked((long)ReadUnsigned(buffer, index, 8));
        /// <inheritdoc />
        public long[] ReadInt64(byte[] buffer, int index, int length) => ReadArray(buffer, index, length, 8, ReadInt64);
        /// <inheritdoc />
        public long[,] ReadInt64(byte[] buffer, int index, int row, int col) => ReadMatrix(buffer, index, row, col, 8, ReadInt64);
        /// <inheritdoc />
        public byte[] GetBytes(long value) => EncodeUnsigned(unchecked((ulong)value), 8);
        /// <inheritdoc />
        public byte[] GetBytes(long[] values) => EncodeArray(values, 8, value => unchecked((ulong)value));

        /// <inheritdoc />
        public ulong ReadUInt64(byte[] buffer, int index) => unchecked((ulong)ReadUnsigned(buffer, index, 8));
        /// <inheritdoc />
        public ulong[] ReadUInt64(byte[] buffer, int index, int length) => ReadArray(buffer, index, length, 8, ReadUInt64);
        /// <inheritdoc />
        public ulong[,] ReadUInt64(byte[] buffer, int index, int row, int col) => ReadMatrix(buffer, index, row, col, 8, ReadUInt64);
        /// <inheritdoc />
        public byte[] GetBytes(ulong value) => EncodeUnsigned(unchecked((ulong)value), 8);
        /// <inheritdoc />
        public byte[] GetBytes(ulong[] values) => EncodeArray(values, 8, value => unchecked((ulong)value));

        /// <inheritdoc />
        public float ReadSingle(byte[] buffer, int index) => BitConverter.ToSingle(BitConverter.GetBytes((uint)ReadUnsigned(buffer, index, 4)), 0);
        /// <inheritdoc />
        public float[] ReadSingle(byte[] buffer, int index, int length) => ReadArray(buffer, index, length, 4, ReadSingle);
        /// <inheritdoc />
        public float[,] ReadSingle(byte[] buffer, int index, int row, int col) => ReadMatrix(buffer, index, row, col, 4, ReadSingle);
        /// <inheritdoc />
        public byte[] GetBytes(float value) => EncodeUnsigned(BitConverter.ToUInt32(BitConverter.GetBytes(value), 0), 4);
        /// <inheritdoc />
        public byte[] GetBytes(float[] values) => EncodeArray(values, 4, value => BitConverter.ToUInt32(BitConverter.GetBytes(value), 0));

        /// <inheritdoc />
        public double ReadDouble(byte[] buffer, int index) => BitConverter.ToDouble(BitConverter.GetBytes((ulong)ReadUnsigned(buffer, index, 8)), 0);
        /// <inheritdoc />
        public double[] ReadDouble(byte[] buffer, int index, int length) => ReadArray(buffer, index, length, 8, ReadDouble);
        /// <inheritdoc />
        public double[,] ReadDouble(byte[] buffer, int index, int row, int col) => ReadMatrix(buffer, index, row, col, 8, ReadDouble);
        /// <inheritdoc />
        public byte[] GetBytes(double value) => EncodeUnsigned(BitConverter.ToUInt64(BitConverter.GetBytes(value), 0), 8);
        /// <inheritdoc />
        public byte[] GetBytes(double[] values) => EncodeArray(values, 8, value => BitConverter.ToUInt64(BitConverter.GetBytes(value), 0));

        /// <inheritdoc />
        public string ReadString(byte[] buffer, int index, int length, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            byte[] bytes = ReadBytes(buffer, index, length);
            return encoding.GetString(SwapStringBytes ? SwapWords(bytes) : bytes);
        }

        /// <inheritdoc />
        public string ReadString(byte[] buffer, Encoding encoding) =>
            ReadString(buffer, 0, buffer?.Length ?? 0, encoding);

        /// <inheritdoc />
        public byte[] GetBytes(string value, Encoding encoding)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));
            byte[] bytes = encoding.GetBytes(value);
            return SwapStringBytes ? SwapWords(bytes) : bytes;
        }

        /// <inheritdoc />
        public byte[] GetBytes(string value, int length, Encoding encoding)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            byte[] result = GetBytes(value, encoding);
            Array.Resize(ref result, length);
            return result;
        }

        private ulong ReadUnsigned(byte[] buffer, int index, int width)
        {
            ValidateRange(buffer, index, 1, width);
            ulong value = 0;
            for (int digit = 0; digit < width; digit++)
                value = (value << 8) | buffer[index + WireIndex(digit, width)];
            return value;
        }

        private void WriteUnsigned(ulong value, byte[] destination, int offset, int width)
        {
            for (int digit = 0; digit < width; digit++)
                destination[offset + WireIndex(digit, width)] = (byte)(value >> (8 * (width - digit - 1)));
        }

        private byte[] EncodeUnsigned(ulong value, int width)
        {
            byte[] result = new byte[width];
            WriteUnsigned(value, result, 0, width);
            return result;
        }

        private int WireIndex(int digit, int width)
        {
            switch (ByteOrder)
            {
                case ByteOrder.BigEndian: return digit;
                case ByteOrder.BigEndianWithByteSwap: return digit ^ 1;
                case ByteOrder.LittleEndianWithByteSwap: return (width - 1 - digit) ^ 1;
                default: return width - 1 - digit;
            }
        }

        private static T[] ReadArray<T>(byte[] buffer, int index, int length, int width, Func<byte[], int, T> read)
        {
            ValidateRange(buffer, index, length, width);
            T[] values = new T[length];
            for (int i = 0; i < length; i++)
                values[i] = read(buffer, index + i * width);
            return values;
        }

        private static T[,] ReadMatrix<T>(byte[] buffer, int index, int row, int col, int width, Func<byte[], int, T> read)
        {
            if (row < 0)
                throw new ArgumentOutOfRangeException(nameof(row));
            if (col < 0 || (long)row * col > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(col));
            ValidateRange(buffer, index, row * col, width);
            T[,] values = new T[row, col];
            for (int r = 0; r < row; r++)
                for (int c = 0; c < col; c++)
                    values[r, c] = read(buffer, index + (r * col + c) * width);
            return values;
        }

        private byte[] EncodeArray<T>(T[] values, int width, Func<T, ulong> bits)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Length > int.MaxValue / width)
                throw new ArgumentOutOfRangeException(nameof(values));
            byte[] result = new byte[values.Length * width];
            for (int i = 0; i < values.Length; i++)
                WriteUnsigned(bits(values[i]), result, i * width, width);
            return result;
        }

        private static void ValidateRange(byte[] buffer, int index, int count, int width)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (index < 0 || index > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (count < 0 || count > (buffer.Length - index) / width)
                throw new ArgumentOutOfRangeException(nameof(count));
        }

        private static void ValidateBits(byte[] buffer, int index, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (index < 0 || index > (long)buffer.Length * 8)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (count < 0 || (long)index + count > (long)buffer.Length * 8 || (long)index + count > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(count));
        }

        private static byte[] SwapWords(byte[] bytes)
        {
            byte[] result = new byte[checked(bytes.Length + bytes.Length % 2)];
            for (int i = 0; i < bytes.Length; i++)
                result[i ^ 1] = bytes[i];
            return result;
        }
    }
}
