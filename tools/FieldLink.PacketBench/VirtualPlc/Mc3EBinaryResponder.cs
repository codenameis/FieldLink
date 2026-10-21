using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>MC 프로토콜 3E 바이너리(Q/L 시리즈)의 서버 역할입니다. 일괄 읽기·쓰기만 지원합니다. (사양서 §3)</summary>
internal sealed class Mc3EBinaryResponder : IVirtualPlcResponder
{
    private const ushort RequestSubheader = 0x0050;
    private const ushort ResponseSubheader = 0x00D0;
    private const int HeaderLength = 9;
    private const int MinBodyLength = 12;
    private const int MaxBodyLength = 2048;
    private const int DataOffset = 21;

    private const ushort CommandBatchRead = 0x0401;
    private const ushort CommandBatchWrite = 0x1401;
    private const ushort SubcommandWordUnit = 0x0000;
    private const ushort SubcommandBitUnit = 0x0001;
    private const ushort SubcommandIqrWordUnit = 0x0002;
    private const ushort SubcommandIqrBitUnit = 0x0003;

    private const int MaxWordPoints = 960;
    private const int MaxBitPoints = 3584;

    private const ushort EndCodeSuccess = 0x0000;
    private const ushort EndCodePointCountExceeded = 0xC051;
    private const ushort EndCodeDeviceRangeExceeded = 0xC056;
    private const ushort EndCodeInvalidCommand = 0xC059;
    private const ushort EndCodeDataLengthMismatch = 0xC061;

    public Mc3EBinaryResponder(Mc3EMemory memory)
    {
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    public Mc3EMemory Memory { get; }

    public string DisplayName => "MC 3E Binary (Q/L)";

    public int DefaultPort => 5000;

    public async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderLength];
        if (!await FrameIo.ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        int bodyLength = header[7] | (header[8] << 8);
        if (bodyLength is < MinBodyLength or > MaxBodyLength)
        {
            return null;
        }

        var frame = new byte[HeaderLength + bodyLength];
        Buffer.BlockCopy(header, 0, frame, 0, HeaderLength);

        if (!await FrameIo.ReadExactlyAsync(stream, frame.AsMemory(HeaderLength), cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return frame;
    }

    public byte[]? Process(byte[] request, VirtualPlcSettings settings)
    {
        if (request.Length < HeaderLength + MinBodyLength || ReadUInt16(request, 0) != RequestSubheader)
        {
            return null;
        }

        int bodyLength = ReadUInt16(request, 7);
        if (bodyLength < MinBodyLength || request.Length != HeaderLength + bodyLength)
        {
            return null;
        }

        ushort command = ReadUInt16(request, 11);
        ushort subcommand = ReadUInt16(request, 13);
        int deviceNumber = request[15] | (request[16] << 8) | (request[17] << 16);
        byte deviceCode = request[18];
        ushort pointCount = ReadUInt16(request, 19);

        try
        {
            byte[] data = Execute(command, subcommand, deviceNumber, deviceCode, pointCount, request);
            return BuildResponse(request, EndCodeSuccess, data);
        }
        catch (McEndCodeException exception)
        {
            return BuildResponse(request, exception.EndCode, Array.Empty<byte>());
        }
    }

    private byte[] Execute(
        ushort command,
        ushort subcommand,
        int deviceNumber,
        byte deviceCode,
        ushort pointCount,
        byte[] request)
    {
        if (!Mc3EDeviceExtensions.TryFromCode(deviceCode, out Mc3EDevice device))
        {
            throw new McEndCodeException(EndCodeInvalidCommand);
        }

        bool isBitUnit = subcommand is SubcommandBitUnit or SubcommandIqrBitUnit;
        bool isWordUnit = subcommand is SubcommandWordUnit or SubcommandIqrWordUnit;
        if (!isBitUnit && !isWordUnit)
        {
            throw new McEndCodeException(EndCodeInvalidCommand);
        }

        if (pointCount == 0)
        {
            throw new McEndCodeException(EndCodePointCountExceeded);
        }

        if (command == CommandBatchRead)
        {
            return ReadDevice(device, deviceNumber, pointCount, isBitUnit);
        }

        if (command == CommandBatchWrite)
        {
            return WriteDevice(device, deviceNumber, pointCount, isBitUnit, request);
        }

        throw new McEndCodeException(EndCodeInvalidCommand);
    }

    private byte[] ReadDevice(Mc3EDevice device, int deviceNumber, ushort pointCount, bool isBitUnit)
    {
        if (device.IsBit() && isBitUnit)
        {
            if (pointCount > MaxBitPoints)
            {
                throw new McEndCodeException(EndCodePointCountExceeded);
            }

            var bits = new bool[pointCount];
            if (!Memory.TryReadBits(device, deviceNumber, pointCount, bits))
            {
                throw new McEndCodeException(EndCodeDeviceRangeExceeded);
            }

            return PackBits(bits);
        }

        if (!device.IsBit() && isBitUnit)
        {
            throw new McEndCodeException(EndCodeInvalidCommand);
        }

        if (pointCount > MaxWordPoints)
        {
            throw new McEndCodeException(EndCodePointCountExceeded);
        }

        var words = new ushort[pointCount];
        bool isRead = device.IsBit()
            ? Memory.TryReadBitDeviceWords(device, deviceNumber, pointCount, words)
            : Memory.TryReadWords(device, deviceNumber, pointCount, words);

        if (!isRead)
        {
            throw new McEndCodeException(EndCodeDeviceRangeExceeded);
        }

        return ToBytes(words);
    }

    private byte[] WriteDevice(Mc3EDevice device, int deviceNumber, ushort pointCount, bool isBitUnit, byte[] request)
    {
        if (!device.IsWritable())
        {
            throw new McEndCodeException(EndCodeInvalidCommand);
        }

        if (device.IsBit() && isBitUnit)
        {
            if (pointCount > MaxBitPoints)
            {
                throw new McEndCodeException(EndCodePointCountExceeded);
            }

            int expectedLength = (pointCount + 1) / 2;
            if (request.Length < DataOffset + expectedLength)
            {
                throw new McEndCodeException(EndCodeDataLengthMismatch);
            }

            bool[] bits = UnpackBits(request, DataOffset, pointCount);
            if (!Memory.TryWriteBits(device, deviceNumber, bits))
            {
                throw new McEndCodeException(EndCodeDeviceRangeExceeded);
            }

            return Array.Empty<byte>();
        }

        if (!device.IsBit() && isBitUnit)
        {
            throw new McEndCodeException(EndCodeInvalidCommand);
        }

        if (pointCount > MaxWordPoints)
        {
            throw new McEndCodeException(EndCodePointCountExceeded);
        }

        if (request.Length < DataOffset + (pointCount * 2))
        {
            throw new McEndCodeException(EndCodeDataLengthMismatch);
        }

        ushort[] words = ReadWords(request, DataOffset, pointCount);
        bool isWritten = device.IsBit()
            ? Memory.TryWriteBitDeviceWords(device, deviceNumber, words)
            : Memory.TryWriteWords(device, deviceNumber, words);

        if (!isWritten)
        {
            throw new McEndCodeException(EndCodeDeviceRangeExceeded);
        }

        return Array.Empty<byte>();
    }

    private static byte[] BuildResponse(byte[] request, ushort endCode, byte[] data)
    {
        var response = new byte[HeaderLength + 2 + data.Length];

        WriteUInt16(response, 0, ResponseSubheader);
        response[2] = request[2];
        response[3] = request[3];
        response[4] = request[4];
        response[5] = request[5];
        response[6] = request[6];
        WriteUInt16(response, 7, (ushort)(2 + data.Length));
        WriteUInt16(response, 9, endCode);

        if (data.Length > 0)
        {
            Array.Copy(data, 0, response, 11, data.Length);
        }

        return response;
    }

    /// <summary>비트 배열을 2점당 1바이트로 묶습니다. 첫 점은 상위 니블(0x10), 둘째 점은 하위 니블(0x01)입니다.</summary>
    private static byte[] PackBits(bool[] bits)
    {
        var packed = new byte[(bits.Length + 1) / 2];
        for (int bitIndex = 0; bitIndex < bits.Length; bitIndex++)
        {
            if (bits[bitIndex])
            {
                packed[bitIndex / 2] |= bitIndex % 2 == 0 ? (byte)0x10 : (byte)0x01;
            }
        }

        return packed;
    }

    private static bool[] UnpackBits(byte[] source, int offset, int count)
    {
        var bits = new bool[count];
        for (int bitIndex = 0; bitIndex < count; bitIndex++)
        {
            byte packed = source[offset + (bitIndex / 2)];
            bits[bitIndex] = bitIndex % 2 == 0 ? (packed & 0x10) != 0 : (packed & 0x01) != 0;
        }

        return bits;
    }

    private static byte[] ToBytes(ushort[] words)
    {
        var bytes = new byte[words.Length * 2];
        for (int i = 0; i < words.Length; i++)
        {
            bytes[i * 2] = (byte)(words[i] & 0xFF);
            bytes[(i * 2) + 1] = (byte)(words[i] >> 8);
        }

        return bytes;
    }

    private static ushort[] ReadWords(byte[] source, int offset, int count)
    {
        var words = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            words[i] = (ushort)(source[offset + (i * 2)] | (source[offset + (i * 2) + 1] << 8));
        }

        return words;
    }

    private static ushort ReadUInt16(byte[] buffer, int offset) => (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)(value >> 8);
    }
}
