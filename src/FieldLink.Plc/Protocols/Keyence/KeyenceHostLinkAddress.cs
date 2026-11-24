using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 프로토콜의 디바이스 주소 표현입니다. (사양서 Part A §A.1)</summary>
public sealed class KeyenceHostLinkAddress
{
    private const int BitsPerChannel = 16;
    private const int MaxBitOffset = BitsPerChannel - 1;
    private const int ChannelDecimalMultiplier = 100;
    private const char BitSeparator = '.';

    private static readonly (string Prefix, bool IsBit, bool IsHex)[] DeviceDefinitions =
    [
        ("CTH", false, false),
        ("CTC", false, false),
        ("MR", true, false),
        ("LR", true, false),
        ("CR", true, false),
        ("VB", true, true),
        ("DM", false, false),
        ("EM", false, false),
        ("FM", false, false),
        ("ZF", false, false),
        ("TM", false, false),
        ("CM", false, false),
        ("VM", false, false),
        ("AT", false, false),
        ("TC", false, false),
        ("TS", false, false),
        ("CC", false, false),
        ("CS", false, false),
        ("R", true, false),
        ("B", true, true),
        ("W", false, true),
        ("Z", false, false),
        ("T", false, false),
        ("C", false, false),
    ];

    private KeyenceHostLinkAddress(string original, string device, int channel, byte bit, bool isBit, bool isHex, string wireString)
    {
        Original = original;
        Device = device;
        Channel = channel;
        Bit = bit;
        IsBit = isBit;
        IsHex = isHex;
        WireString = wireString;
    }

    /// <summary>입력된 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>디바이스 접두사입니다. (예: "DM", "MR", "R", "W")</summary>
    public string Device { get; }

    /// <summary>채널(또는 워드) 번호입니다.</summary>
    public int Channel { get; }

    /// <summary>채널 내 비트 번호 (0~15)입니다.</summary>
    public byte Bit { get; }

    /// <summary>비트 디바이스 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>16진수 주소 체계 여부입니다. (B, VB, W)</summary>
    public bool IsHex { get; }

    /// <summary>상위 링크 와이어 주소 문자열입니다. (예: "DM100", "MR100")</summary>
    public string WireString { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static KeyenceHostLinkAddress Parse(string address)
    {
        if (!TryParse(address, out KeyenceHostLinkAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        [NotNullWhen(true)] out KeyenceHostLinkAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            errorMessage = "주소 문자열은 필수입니다.";
            return false;
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (!TryExtractDevice(trimmed, out string devicePrefix, out bool isBit, out bool isHex, out string numberText))
        {
            errorMessage = $"지원하지 않는 Keyence 디바이스 접두사입니다: {address}";
            return false;
        }

        return isBit
            ? TryParseBitAddress(trimmed, devicePrefix, isHex, numberText, out result, out errorMessage)
            : TryParseWordAddress(trimmed, devicePrefix, isHex, numberText, out result, out errorMessage);
    }

    private static bool TryExtractDevice(
        string trimmed,
        out string devicePrefix,
        out bool isBit,
        out bool isHex,
        out string numberText)
    {
        devicePrefix = string.Empty;
        isBit = false;
        isHex = false;
        numberText = string.Empty;

        foreach ((string prefix, bool prefixIsBit, bool prefixIsHex) in DeviceDefinitions)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                devicePrefix = prefix;
                numberText = trimmed.Substring(prefix.Length);
                isBit = prefixIsBit;
                isHex = prefixIsHex;
                return numberText.Length > 0;
            }
        }

        return false;
    }

    private static bool TryParseBitAddress(
        string original,
        string devicePrefix,
        bool isHex,
        string numberText,
        [NotNullWhen(true)] out KeyenceHostLinkAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        int dot = numberText.IndexOf(BitSeparator);
        if (dot >= 0)
        {
            return TryParseDotBit(original, devicePrefix, isHex, numberText, dot, out result, out errorMessage);
        }

        if (isHex)
        {
            if (!int.TryParse(numberText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int hexVal))
            {
                errorMessage = $"16진 비트 주소 파싱 실패: {original}";
                return false;
            }

            string wire = $"{devicePrefix}{hexVal:X}";
            result = new KeyenceHostLinkAddress(original, devicePrefix, hexVal, 0, isBit: true, isHex: true, wire);
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int decVal))
        {
            errorMessage = $"10진 비트 주소 파싱 실패: {original}";
            return false;
        }

        int channel = decVal < BitsPerChannel ? 0 : decVal / ChannelDecimalMultiplier;
        int bitValue = decVal < BitsPerChannel ? decVal : decVal % ChannelDecimalMultiplier;
        if (bitValue > MaxBitOffset)
        {
            errorMessage = $"비트 오프셋(0~{MaxBitOffset}) 오류: {original}";
            return false;
        }

        byte bit = (byte)bitValue;
        string wireDec = decVal < BitsPerChannel ? $"{devicePrefix}{decVal}" : $"{devicePrefix}{channel}{bit:D2}";
        result = new KeyenceHostLinkAddress(original, devicePrefix, channel, bit, isBit: true, isHex: false, wireDec);
        return true;
    }

    private static bool TryParseDotBit(
        string original,
        string devicePrefix,
        bool isHex,
        string numberText,
        int dot,
        [NotNullWhen(true)] out KeyenceHostLinkAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string chStr = numberText.Substring(0, dot);
        string bitStr = numberText.Substring(dot + 1);

        int ch;
        if (isHex)
        {
            if (!int.TryParse(chStr, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ch))
            {
                errorMessage = $"16진 채널 번호 오류: {original}";
                return false;
            }
        }
        else if (!int.TryParse(chStr, NumberStyles.None, CultureInfo.InvariantCulture, out ch))
        {
            errorMessage = $"10진 채널 번호 오류: {original}";
            return false;
        }

        if (!byte.TryParse(bitStr, NumberStyles.None, CultureInfo.InvariantCulture, out byte bit) || bit > MaxBitOffset)
        {
            errorMessage = $"비트 오프셋(0~{MaxBitOffset}) 오류: {original}";
            return false;
        }

        string wire = isHex ? $"{devicePrefix}{ch:X}{BitSeparator}{bit}" : $"{devicePrefix}{ch}{bit:D2}";
        result = new KeyenceHostLinkAddress(original, devicePrefix, ch, bit, isBit: true, isHex, wire);
        return true;
    }

    private static bool TryParseWordAddress(
        string original,
        string devicePrefix,
        bool isHex,
        string numberText,
        [NotNullWhen(true)] out KeyenceHostLinkAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        int parsedNumber;
        if (isHex)
        {
            if (!int.TryParse(numberText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out parsedNumber))
            {
                errorMessage = $"16진 워드 주소 파싱 실패: {original}";
                return false;
            }
        }
        else if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out parsedNumber))
        {
            errorMessage = $"10진 워드 주소 파싱 실패: {original}";
            return false;
        }

        string wire = isHex ? $"{devicePrefix}{parsedNumber:X}" : $"{devicePrefix}{parsedNumber}";
        result = new KeyenceHostLinkAddress(original, devicePrefix, parsedNumber, 0, isBit: false, isHex, wire);
        return true;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="KeyenceHostLinkAddress"/>를 반환합니다.</summary>
    public KeyenceHostLinkAddress Offset(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        if (IsBit && !IsHex)
        {
            int totalBit = checked((Channel * BitsPerChannel) + Bit + count);
            int newCh = totalBit / BitsPerChannel;
            byte newBit = (byte)(totalBit % BitsPerChannel);
            string wire = totalBit < BitsPerChannel ? $"{Device}{totalBit}" : $"{Device}{newCh}{newBit:D2}";
            return new KeyenceHostLinkAddress($"{Original}+{count}", Device, newCh, newBit, isBit: true, isHex: false, wire);
        }

        int newNum = checked(Channel + count);
        string newWire = IsHex ? $"{Device}{newNum:X}" : $"{Device}{newNum}";
        return new KeyenceHostLinkAddress($"{Original}+{count}", Device, newNum, 0, IsBit, IsHex, newWire);
    }
}
