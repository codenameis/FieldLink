using System;
using System.Collections.Generic;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 바이너리 요청 프레임(3E·4E)을 만드는 순수 함수 모음입니다. (사양서 §3-1, §3-3)</summary>
public static class MCBinaryRequestBuilder
{
    private const ushort Request3ESubheader = 0x0050;
    private const ushort Request4ESubheader = 0x0054;
    private const byte BitOnNibbleHigh = 0x10;
    private const byte BitOnNibbleLow = 0x01;

    /// <summary>디바이스 일괄 읽기 요청 프레임을 만듭니다.</summary>
    /// <param name="options">프레임 종류·시리즈·액세스 경로 설정입니다.</param>
    /// <param name="serialNumber">4E 프레임의 시리얼 번호입니다. 3E에서는 쓰이지 않습니다.</param>
    /// <param name="address">읽기 시작 주소입니다.</param>
    /// <param name="pointCount">읽을 점수입니다. 비트 디바이스는 비트 수, 워드 디바이스는 워드 수입니다.</param>
    public static byte[] BuildReadRequest(MCOptions options, ushort serialNumber, MCAddress address, ushort pointCount)
    {
        ValidateRequest(options, address, pointCount);
        byte[] body = BuildBody(options, MCCommand.BatchRead, address, pointCount, Array.Empty<byte>());
        return WrapWithHeader(options, serialNumber, body);
    }

    /// <summary>디바이스 일괄 쓰기 요청 프레임을 만듭니다.</summary>
    /// <param name="options">프레임 종류·시리즈·액세스 경로 설정입니다.</param>
    /// <param name="serialNumber">4E 프레임의 시리얼 번호입니다. 3E에서는 쓰이지 않습니다.</param>
    /// <param name="address">쓰기 시작 주소입니다.</param>
    /// <param name="pointCount">쓸 점수입니다.</param>
    /// <param name="data">워드 디바이스는 점수 × 2바이트(리틀 엔디언), 비트 디바이스는 점수 × 1바이트(0 또는 1)입니다.</param>
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

        byte[] payload = address.IsBit ? PackBitNibbles(data) : (byte[])data.Clone();
        byte[] body = BuildBody(options, MCCommand.BatchWrite, address, pointCount, payload);
        return WrapWithHeader(options, serialNumber, body);
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

    private static byte[] BuildBody(MCOptions options, MCCommand command, MCAddress address, ushort pointCount, byte[] payload)
    {
        var body = new List<byte>();
        AddUInt16(body, options.MonitoringTimer);
        AddUInt16(body, (ushort)command);
        AddUInt16(body, (ushort)options.Series.GetSubcommand(address.IsBit));
        AddLittleEndian(body, address.DeviceNumber, options.Series.GetDeviceNumberByteCount());
        AddLittleEndian(body, address.Device.Code, options.Series.GetDeviceCodeByteCount());
        AddUInt16(body, pointCount);
        body.AddRange(payload);
        return body.ToArray();
    }

    private static byte[] WrapWithHeader(MCOptions options, ushort serialNumber, byte[] body)
    {
        var frame = new List<byte>();
        bool is4E = options.FrameType == MCFrameType.Frame4E;

        AddUInt16(frame, is4E ? Request4ESubheader : Request3ESubheader);
        if (is4E)
        {
            AddUInt16(frame, serialNumber);
            AddUInt16(frame, 0x0000); // 예약
        }

        frame.Add(options.NetworkNumber);
        frame.Add(options.PcNumber);
        AddUInt16(frame, options.ModuleIoNumber);
        frame.Add(options.ModuleStationNumber);
        AddUInt16(frame, (ushort)body.Length);
        frame.AddRange(body);
        return frame.ToArray();
    }

    private static byte[] PackBitNibbles(byte[] bitValues)
    {
        // 사양서 §4: 첫 번째 점은 상위 니블, 두 번째 점은 하위 니블. 홀수 개면 마지막 하위 니블은 0.
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

    private static void AddUInt16(List<byte> buffer, ushort value)
    {
        buffer.Add((byte)value);
        buffer.Add((byte)(value >> 8));
    }

    private static void AddLittleEndian(List<byte> buffer, uint value, int byteCount)
    {
        for (int byteIndex = 0; byteIndex < byteCount; byteIndex++)
        {
            buffer.Add((byte)(value >> (8 * byteIndex)));
        }
    }
}
