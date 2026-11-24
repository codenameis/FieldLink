using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 주소 표현입니다. (사양서 Part A §A.1.2)</summary>
public sealed class KeyenceHostLinkAddress
{
    private const int BitsPerChannel = 16;
    private const int MaxBitOffsetInChannel = BitsPerChannel - 1;
    private const int DecimalChannelBitDivisor = 100;
    private const int SingleCharPrefixLength = 1;
    private const char BitSeparator = '.';

    private static readonly string[] MultiCharPrefixes =
    [
        "CTH", "CTC", "MR", "LR", "CR", "VB", "DM", "EM", "FM", "ZF", "TM", "CM", "VM", "AT", "TC", "TS", "CC", "CS"
    ];

    private static readonly string[] SingleCharPrefixes = ["R", "B", "W", "Z", "T", "C"];

    private KeyenceHostLinkAddress(
        string original,
        string device,
        int channel,
        byte bit,
        bool isBit,
        bool isHex,
        string wireString)
    {
        Original = original;
        Device = device;
        Channel = channel;
        Bit = bit;
        IsBit = isBit;
        IsHex = isHex;
        WireString = wireString;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>디바이스 접두사입니다. (예: "DM", "MR", "R", "W")</summary>
    public string Device { get; }

    /// <summary>10진 또는 16진 번호(비트 채널 포함)입니다.</summary>
    public int Channel { get; }

    /// <summary>채널 내 비트 번호(0~15)입니다.</summary>
    public byte Bit { get; }

    /// <summary>비트 접점 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>16진 번호 체계 디바이스 여부입니다. (B, VB, W)</summary>
    public bool IsHex { get; }

    /// <summary>상위 링크 와이어 주소 문자열입니다. (예: "DM100", "MR100")</summary>
    public string WireString { get; }

    /// <summary>타이머/카운터 복합 응답("접점,현재값[,설정값]") 디바이스 여부입니다.</summary>
    public bool IsCompositeDevice => Device is "T" or "C" or "CTH" or "CTC";

    /// <summary>32비트(4바이트) 단위로 응답하는 디바이스 여부입니다.</summary>
    public bool Is32BitDevice => Device is "Z" or "AT" or "TC" or "TS" or "CC" or "CS" or "T" or "C" or "CTH" or "CTC";

    internal int MaxElementsPerRequest => GetDeviceSpecification(Device).MaxElementsPerRequest;

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static KeyenceHostLinkAddress Parse(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

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
        [NotNullWhen(false)] out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            errorMessage = "주소 문자열은 필수입니다.";
            return false;
        }

        string trimmed = address.Trim().ToUpperInvariant();
        if (!TryExtractPrefix(trimmed, out string devicePrefix, out bool isBit, out bool isHex, out string numberText))
        {
            errorMessage = $"지원하지 않는 Keyence 디바이스입니다: {address}";
            return false;
        }

        if (isBit)
        {
            return TryParseBit(trimmed, devicePrefix, isHex, numberText, out result, out errorMessage);
        }

        return TryParseWord(trimmed, devicePrefix, isHex, numberText, out result, out errorMessage);
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
            long totalBitIndexLong = ((long)Channel * BitsPerChannel) + Bit + count;
            if (totalBitIndexLong > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "비트 주소 오프셋이 허용 범위를 초과합니다.");
            }

            int totalBitIndex = (int)totalBitIndexLong;
            int nextChannel = totalBitIndex / BitsPerChannel;
            byte nextBit = (byte)(totalBitIndex % BitsPerChannel);
            string bitWireString = FormatDecimalBitWireString(Device, nextChannel, nextBit);
            return new KeyenceHostLinkAddress($"{Original}+{count}", Device, nextChannel, nextBit, isBit: true, isHex: false, bitWireString);
        }

        long nextNumberLong = (long)Channel + count;
        (int minAddress, int maxAddress, _) = GetDeviceSpecification(Device);
        if (nextNumberLong < minAddress || nextNumberLong > maxAddress)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"{Device} 주소 번호({nextNumberLong})가 허용 범위({minAddress}~{maxAddress})를 벗어났습니다.");
        }

        int nextNumber = (int)nextNumberLong;
        string nextWireString = IsHex
            ? string.Format(CultureInfo.InvariantCulture, "{0}{1:X}", Device, nextNumber)
            : string.Format(CultureInfo.InvariantCulture, "{0}{1}", Device, nextNumber);
        return new KeyenceHostLinkAddress($"{Original}+{count}", Device, nextNumber, 0, IsBit, IsHex, nextWireString);
    }

    private static bool TryExtractPrefix(
        string trimmed,
        out string devicePrefix,
        out bool isBit,
        out bool isHex,
        out string numberText)
    {
        foreach (string prefixCandidate in MultiCharPrefixes)
        {
            if (trimmed.StartsWith(prefixCandidate, StringComparison.Ordinal))
            {
                devicePrefix = prefixCandidate;
                numberText = trimmed.Substring(prefixCandidate.Length);
                isBit = prefixCandidate is "MR" or "LR" or "CR" or "VB";
                isHex = prefixCandidate is "VB";
                return numberText.Length > 0;
            }
        }

        foreach (string prefixCandidate in SingleCharPrefixes)
        {
            if (trimmed.StartsWith(prefixCandidate, StringComparison.Ordinal))
            {
                devicePrefix = prefixCandidate;
                numberText = trimmed.Substring(SingleCharPrefixLength);
                isBit = prefixCandidate is "R" or "B";
                isHex = prefixCandidate is "B" or "W";
                return numberText.Length > 0;
            }
        }

        devicePrefix = string.Empty;
        isBit = false;
        isHex = false;
        numberText = string.Empty;
        return false;
    }

    private static bool TryParseBit(
        string original,
        string devicePrefix,
        bool isHex,
        string numberText,
        [NotNullWhen(true)] out KeyenceHostLinkAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        int dotIndex = numberText.IndexOf(BitSeparator);
        if (dotIndex >= 0)
        {
            if (isHex)
            {
                errorMessage = $"16진 비트 디바이스({devicePrefix})는 점(.) 비트 표기를 지원하지 않습니다: {original}";
                return false;
            }

            return TryParseDotBit(original, devicePrefix, numberText, dotIndex, out result, out errorMessage);
        }

        if (isHex)
        {
            if (!long.TryParse(numberText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long hexValue))
            {
                errorMessage = $"16진 비트 주소 파싱 실패: {original}";
                return false;
            }

            (int minAddress, int maxAddress, _) = GetDeviceSpecification(devicePrefix);
            if (hexValue < minAddress || hexValue > maxAddress)
            {
                errorMessage = $"{devicePrefix} 주소 범위(0x{minAddress:X}~0x{maxAddress:X})를 벗어났습니다: {original}";
                return false;
            }

            string hexWireString = string.Format(CultureInfo.InvariantCulture, "{0}{1:X}", devicePrefix, (int)hexValue);
            result = new KeyenceHostLinkAddress(original, devicePrefix, (int)hexValue, 0, isBit: true, isHex: true, hexWireString);
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int decimalValue))
        {
            errorMessage = $"10진 비트 주소 파싱 실패: {original}";
            return false;
        }

        int channel = decimalValue < DecimalChannelBitDivisor ? 0 : decimalValue / DecimalChannelBitDivisor;
        int bitOffset = decimalValue < DecimalChannelBitDivisor ? decimalValue : decimalValue % DecimalChannelBitDivisor;
        if (bitOffset > MaxBitOffsetInChannel)
        {
            errorMessage = $"채널 내 비트 번호(00~15) 범위를 벗어났습니다: {original}";
            return false;
        }

        byte bit = (byte)bitOffset;
        string decimalWireString = FormatDecimalBitWireString(devicePrefix, channel, bit);
        result = new KeyenceHostLinkAddress(original, devicePrefix, channel, bit, isBit: true, isHex: false, decimalWireString);
        return true;
    }

    private static bool TryParseDotBit(
        string original,
        string devicePrefix,
        string numberText,
        int dotIndex,
        [NotNullWhen(true)] out KeyenceHostLinkAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string channelText = numberText.Substring(0, dotIndex);
        string bitText = numberText.Substring(dotIndex + 1);

        if (!int.TryParse(channelText, NumberStyles.None, CultureInfo.InvariantCulture, out int channel))
        {
            errorMessage = $"10진 채널 번호 오류: {original}";
            return false;
        }

        if (!byte.TryParse(bitText, NumberStyles.None, CultureInfo.InvariantCulture, out byte bit)
            || bit > MaxBitOffsetInChannel)
        {
            errorMessage = $"비트 오프셋(0~{MaxBitOffsetInChannel}) 오류: {original}";
            return false;
        }

        if ((long)channel * BitsPerChannel + bit > int.MaxValue)
        {
            errorMessage = $"비트 주소가 허용 범위를 초과했습니다: {original}";
            return false;
        }

        string wireString = FormatDecimalBitWireString(devicePrefix, channel, bit);
        result = new KeyenceHostLinkAddress(original, devicePrefix, channel, bit, isBit: true, isHex: false, wireString);
        return true;
    }

    private static bool TryParseWord(
        string original,
        string devicePrefix,
        bool isHex,
        string numberText,
        [NotNullWhen(true)] out KeyenceHostLinkAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        NumberStyles numberStyle = isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
        if (!long.TryParse(numberText, numberStyle, CultureInfo.InvariantCulture, out long parsedNumber))
        {
            errorMessage = $"{(isHex ? "16진" : "10진")} 워드 주소 파싱 실패: {original}";
            return false;
        }

        (int minAddress, int maxAddress, _) = GetDeviceSpecification(devicePrefix);
        if (parsedNumber < minAddress || parsedNumber > maxAddress)
        {
            string rangeText = isHex
                ? $"0x{minAddress:X}~0x{maxAddress:X}"
                : $"{minAddress}~{maxAddress}";
            errorMessage = $"{devicePrefix} 주소 범위({rangeText})를 벗어났습니다: {original}";
            return false;
        }

        int addressNumber = (int)parsedNumber;
        string wireString = isHex
            ? string.Format(CultureInfo.InvariantCulture, "{0}{1:X}", devicePrefix, addressNumber)
            : string.Format(CultureInfo.InvariantCulture, "{0}{1}", devicePrefix, addressNumber);
        result = new KeyenceHostLinkAddress(original, devicePrefix, addressNumber, 0, isBit: false, isHex, wireString);
        return true;
    }

    private static string FormatDecimalBitWireString(string devicePrefix, int channel, byte bit)
    {
        return channel == 0
            ? string.Format(CultureInfo.InvariantCulture, "{0}{1}", devicePrefix, bit)
            : string.Format(CultureInfo.InvariantCulture, "{0}{1}{2:D2}", devicePrefix, channel, bit);
    }

    private static (int MinAddress, int MaxAddress, int MaxElementsPerRequest) GetDeviceSpecification(string devicePrefix) => devicePrefix switch
    {
        "R" or "MR" or "LR" or "CR" => (0, int.MaxValue, 1000),
        "B" => (0, 0x7FFF, 1000),
        "VB" => (0, 0xF9FF, 1000),
        "W" => (0, 0x7FFF, 1000),
        "DM" or "EM" => (0, 65534, 1000),
        "FM" => (0, 32767, 1000),
        "ZF" => (0, 524287, 1000),
        "TM" => (0, 511, 512),
        "CM" => (0, 11998, 1000),
        "VM" => (0, 59999, 1000),
        "Z" => (1, 12, 12),
        "AT" => (0, 7, 8),
        "T" or "TC" or "TS" or "C" or "CC" or "CS" => (0, 3999, 64),
        "CTH" => (0, 1, 2),
        "CTC" => (0, 3, 4),
        _ => (0, int.MaxValue, 1000),
    };
}
