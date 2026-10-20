using System;
using System.Globalization;
using System.Text;

namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 ASCII 요청 프레임(3E·4E)을 만드는 순수 함수 모음입니다. (사양서 §3-1, §3-3)</summary>
public static class MCAsciiRequestBuilder
{
    private const string Request3ESubheader = "5000";
    private const string Request4ESubheader = "5400";
    private const string Reserved = "0000";
    private const char DeviceCodePadding = '*';

    /// <summary>디바이스 일괄 읽기 요청 프레임을 만듭니다.</summary>
    /// <param name="options">프레임 종류·시리즈·액세스 경로 설정입니다.</param>
    /// <param name="serialNumber">4E 프레임의 시리얼 번호입니다. 3E에서는 쓰이지 않습니다.</param>
    /// <param name="address">읽기 시작 주소입니다.</param>
    /// <param name="pointCount">읽을 점수입니다.</param>
    public static byte[] BuildReadRequest(MCOptions options, ushort serialNumber, MCAddress address, ushort pointCount)
    {
        ValidateRequest(options, address, pointCount);
        string body = BuildBody(options, MCCommand.BatchRead, address, pointCount, string.Empty);
        return Encoding.ASCII.GetBytes(WrapWithHeader(options, serialNumber, body));
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

        string payload = address.IsBit ? FormatBits(data) : FormatWords(data);
        string body = BuildBody(options, MCCommand.BatchWrite, address, pointCount, payload);
        return Encoding.ASCII.GetBytes(WrapWithHeader(options, serialNumber, body));
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

    private static string BuildBody(MCOptions options, MCCommand command, MCAddress address, ushort pointCount, string payload)
    {
        var body = new StringBuilder();
        body.Append(FormatHex(options.MonitoringTimer, 4));
        body.Append(FormatHex((ushort)command, 4));
        body.Append(FormatHex((ushort)options.Series.GetSubcommand(address.IsBit), 4));
        // 사양서 §3-3: ASCII에서는 바이너리와 달리 디바이스 코드가 번호보다 먼저 온다.
        body.Append(address.Device.Symbol.PadRight(options.Series.GetDeviceCodeCharacterCount(), DeviceCodePadding));
        body.Append(FormatDeviceNumber(address, options.Series.GetDeviceNumberCharacterCount()));
        body.Append(FormatHex(pointCount, 4));
        body.Append(payload);
        return body.ToString();
    }

    private static string WrapWithHeader(MCOptions options, ushort serialNumber, string body)
    {
        var frame = new StringBuilder();
        bool is4E = options.FrameType == MCFrameType.Frame4E;

        frame.Append(is4E ? Request4ESubheader : Request3ESubheader);
        if (is4E)
        {
            frame.Append(FormatHex(serialNumber, 4));
            frame.Append(Reserved);
        }

        frame.Append(FormatHex(options.NetworkNumber, 2));
        frame.Append(FormatHex(options.PcNumber, 2));
        frame.Append(FormatHex(options.ModuleIoNumber, 4));
        frame.Append(FormatHex(options.ModuleStationNumber, 2));
        frame.Append(FormatHex((uint)body.Length, 4));
        frame.Append(body);
        return frame.ToString();
    }

    private static string FormatDeviceNumber(MCAddress address, int characterCount)
    {
        string format = address.Device.NumberBase == 16 ? "X" : "D";
        return address.DeviceNumber.ToString(format + characterCount, CultureInfo.InvariantCulture);
    }

    private static string FormatWords(byte[] littleEndianWords)
    {
        // 사양서 §4: 워드 하나는 상위 바이트 2문자 + 하위 바이트 2문자 순서로 쓴다.
        var text = new StringBuilder(littleEndianWords.Length * 2);
        for (int wordIndex = 0; wordIndex < littleEndianWords.Length; wordIndex += 2)
        {
            int word = littleEndianWords[wordIndex] | (littleEndianWords[wordIndex + 1] << 8);
            text.Append(FormatHex((uint)word, 4));
        }

        return text.ToString();
    }

    private static string FormatBits(byte[] bitValues)
    {
        var text = new StringBuilder(bitValues.Length);
        foreach (byte bitValue in bitValues)
        {
            text.Append(bitValue != 0 ? '1' : '0');
        }

        return text.ToString();
    }

    private static string FormatHex(uint value, int digitCount)
    {
        return value.ToString("X" + digitCount, CultureInfo.InvariantCulture);
    }
}
