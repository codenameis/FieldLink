using System;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 바이너리 요청 프레임(3E·4E)을 만드는 순수 함수 모음입니다. (사양서 §3-1, §3-3)</summary>
public static class MCBinaryRequestBuilder
{
    private const ushort Request3ESubheader = 0x0050;
    private const ushort Request4ESubheader = 0x0054;
    private const byte BitOnNibbleHigh = 0x10;
    private const byte BitOnNibbleLow = 0x01;

    /// <summary>디바이스 일괄 읽기 요청 프레임을 만듭니다.</summary>
    public static byte[] BuildReadRequest(MCOptions options, ushort serialNumber, MCAddress address, ushort pointCount)
    {
        ValidateRequest(options, address, pointCount);
        return BuildFrame(options, serialNumber, MCCommand.BatchRead, address, pointCount, Array.Empty<byte>());
    }

    /// <summary>디바이스 일괄 쓰기 요청 프레임을 만듭니다.</summary>
    public static byte[] BuildWriteRequest(
        MCOptions options,
        ushort serialNumber,
        MCAddress address,
        ushort pointCount,
        byte[] data)
    {
        ValidateRequest(options, address, pointCount);
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        int expectedLength = address.IsBit ? pointCount : pointCount * 2;
        if (data.Length != expectedLength)
        {
            throw new ArgumentException($"쓰기 데이터는 {expectedLength}바이트여야 합니다. (실제: {data.Length})", nameof(data));
        }

        byte[] payload = address.IsBit ? PackBitNibbles(data) : data;
        return BuildFrame(options, serialNumber, MCCommand.BatchWrite, address, pointCount, payload);
    }

    private static void ValidateRequest(MCOptions options, MCAddress address, ushort pointCount)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (pointCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointCount), pointCount, "점수는 1 이상이어야 합니다.");
        }

        if (address.DeviceNumber > options.Series.GetMaxDeviceNumber())
        {
            throw new ArgumentOutOfRangeException(nameof(address), address.DeviceNumber, "시리즈가 표현할 수 있는 디바이스 번호를 넘었습니다.");
        }
    }

    private static byte[] BuildFrame(
        MCOptions options,
        ushort serialNumber,
        MCCommand command,
        MCAddress address,
        ushort pointCount,
        byte[] payload)
    {
        bool is4E = options.FrameType == MCFrameType.Frame4E;
        int headerLength = is4E ? 13 : 9;
        int devNumBytes = options.Series.GetDeviceNumberByteCount();
        int devCodeBytes = options.Series.GetDeviceCodeByteCount();
        int bodyLength = 6 + devNumBytes + devCodeBytes + 2 + payload.Length;

        byte[] frame = new byte[headerLength + bodyLength];
        int offset = 0;

        // 1. 서브헤더 (3E: 50 00, 4E: 54 00 + Serial + Reserved)
        BinaryBuffer.WriteUInt16LE(frame, offset, is4E ? Request4ESubheader : Request3ESubheader);
        offset += 2;

        if (is4E)
        {
            BinaryBuffer.WriteUInt16LE(frame, offset, serialNumber);
            BinaryBuffer.WriteUInt16LE(frame, offset + 2, 0x0000);
            offset += 4;
        }

        // 2. 액세스 경로 (5 Bytes) 및 요청 데이터 길이 (2 Bytes)
        frame[offset++] = options.NetworkNumber;
        frame[offset++] = options.PcNumber;
        BinaryBuffer.WriteUInt16LE(frame, offset, options.ModuleIoNumber);
        offset += 2;
        frame[offset++] = options.ModuleStationNumber;
        BinaryBuffer.WriteUInt16LE(frame, offset, (ushort)bodyLength);
        offset += 2;

        // 3. 감시 타이머, 커맨드, 서브커맨드 (6 Bytes)
        BinaryBuffer.WriteUInt16LE(frame, offset, options.MonitoringTimer);
        BinaryBuffer.WriteUInt16LE(frame, offset + 2, (ushort)command);
        BinaryBuffer.WriteUInt16LE(frame, offset + 4, (ushort)options.Series.GetSubcommand(address.IsBit));
        offset += 6;

        // 4. 선두 디바이스 번호, 디바이스 코드, 디바이스 점수
        WriteLittleEndian(frame, ref offset, address.DeviceNumber, devNumBytes);
        WriteLittleEndian(frame, ref offset, address.Device.Code, devCodeBytes);
        BinaryBuffer.WriteUInt16LE(frame, offset, pointCount);
        offset += 2;

        // 5. 쓰기 데이터 페이로드
        if (payload.Length > 0)
        {
            Buffer.BlockCopy(payload, 0, frame, offset, payload.Length);
        }

        return frame;
    }

    private static byte[] PackBitNibbles(byte[] bitValues)
    {
        var packed = new byte[(bitValues.Length + 1) / 2];
        for (int bitIndex = 0; bitIndex < bitValues.Length; bitIndex++)
        {
            if (bitValues[bitIndex] != 0)
            {
                packed[bitIndex / 2] |= bitIndex % 2 == 0 ? BitOnNibbleHigh : BitOnNibbleLow;
            }
        }

        return packed;
    }

    private static void WriteLittleEndian(byte[] buffer, ref int offset, uint value, int byteCount)
    {
        for (int byteIndex = 0; byteIndex < byteCount; byteIndex++)
        {
            buffer[offset++] = (byte)(value >> (8 * byteIndex));
        }
    }
}
