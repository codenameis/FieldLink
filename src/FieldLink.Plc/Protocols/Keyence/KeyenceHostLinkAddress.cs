using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 프로토콜의 디바이스 주소 표현입니다. (사양서 Part A §A.1)</summary>
public sealed class KeyenceHostLinkAddress
{
    private const int BitsPerChannel = 16;
    private const int MaxBitOffsetInChannel = 15;
    private const int DecimalChannelBitDivisor = 100;

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

    /// <summary>타이머/카운터 복합 응답("접점,현재값[,설정값]") 디바이스 여부입니다.</summary>
    public bool IsCompositeDevice => Device is "T" or "C" or "CTH" or "CTC";

    /// <summary>32비트(4바이트) 단위로 응답하는 디바이스 여부입니다.</summary>
    public bool Is32BitDevice => Device is "Z" or "AT" or "TC" or "TS" or "CC" or "CS" or "T" or "C" or "CTH" or "CTC";

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
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
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
        foreach (string prefixCandidate in MultiCharPrefixes)
        {
            if (trimmed.StartsWith(prefixCandidate, StringComparison.Ordinal))
            {
                devicePrefix = prefixCandidate;
                numberText = trimmed.Substring(prefixCandidate.Length);
                isBit = prefixCandidate is "MR" or "LR" or "CR" or "VB";
                isHex = prefixCandidate is "VB";
                return true;
            }
        }

        foreach (string prefixCandidate in SingleCharPrefixes)
        {
            if (trimmed.StartsWith(prefixCandidate, StringComparison.Ordinal))
            {
                devicePrefix = prefixCandidate;
                numberText = trimmed.Substring(1);
                isBit = prefixCandidate is "R" or "B";
                isHex = prefixCandidate is "B" or "W";
                return true;
            }
        }

        devicePrefix = string.Empty;
        isBit = false;
        isHex = false;
        numberText = string.Empty;
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

        int dotIndex = numberText.IndexOf('.');
        if (dotIndex >= 0)
        {
            return TryParseDotBit(original, devicePrefix, isHex, numberText, dotIndex, out result, out errorMessage);
        }

        if (isHex)
        {
            if (!int.TryParse(numberText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexValue) || hexValue < 0)
            {
                errorMessage = $"16진 비트 주소 파싱 실패: {original}";
                return false;
            }

            string hexWireString = $"{devicePrefix}{hexValue:X}";
            result = new KeyenceHostLinkAddress(original, devicePrefix, hexValue, 0, isBit: true, isHex: true, hexWireString);
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
        string decimalWireString = channel == 0 && decimalValue < BitsPerChannel
            ? $"{devicePrefix}{bit}"
            : $"{devicePrefix}{channel}{bit:D2}";
        result = new KeyenceHostLinkAddress(original, devicePrefix, channel, bit, isBit: true, isHex: false, decimalWireString);
        return true;
    }

    private static bool TryParseDotBit(
        string original,
        string devicePrefix,
        bool isHex,
        string numberText,
        int dotIndex,
        [NotNullWhen(true)] out KeyenceHostLinkAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string channelText = numberText.Substring(0, dotIndex);
        string bitText = numberText.Substring(dotIndex + 1);

        int channel;
        if (isHex)
        {
            if (!int.TryParse(channelText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out channel) || channel < 0)
            {
                errorMessage = $"16진 채널 번호 오류: {original}";
                return false;
            }
        }
        else if (!int.TryParse(channelText, NumberStyles.None, CultureInfo.InvariantCulture, out channel))
        {
            errorMessage = $"10진 채널 번호 오류: {original}";
            return false;
        }

        if (!byte.TryParse(bitText, NumberStyles.None, CultureInfo.InvariantCulture, out byte bit)
            || bit > MaxBitOffsetInChannel)
        {
            errorMessage = $"비트 오프셋(0~15) 오류: {original}";
            return false;
        }

        string wireString = isHex ? $"{devicePrefix}{channel:X}.{bit}" : $"{devicePrefix}{channel}{bit:D2}";
        result = new KeyenceHostLinkAddress(original, devicePrefix, channel, bit, isBit: true, isHex, wireString);
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
            if (!int.TryParse(numberText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsedNumber) || parsedNumber < 0)
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

        string wireString = isHex ? $"{devicePrefix}{parsedNumber:X}" : $"{devicePrefix}{parsedNumber}";
        result = new KeyenceHostLinkAddress(original, devicePrefix, parsedNumber, 0, isBit: false, isHex, wireString);
        return true;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="KeyenceHostLinkAddress"/>를 반환합니다.</summary>
    public KeyenceHostLinkAddress Offset(int count)
    {
        if (IsBit && !IsHex)
        {
            int totalBitIndex = (Channel * BitsPerChannel) + Bit + count;
            int nextChannel = totalBitIndex / BitsPerChannel;
            byte nextBit = (byte)(totalBitIndex % BitsPerChannel);
            string bitWireString = totalBitIndex < BitsPerChannel
                ? $"{Device}{totalBitIndex}"
                : $"{Device}{nextChannel}{nextBit:D2}";
            return new KeyenceHostLinkAddress($"{Original}+{count}", Device, nextChannel, nextBit, isBit: true, isHex: false, bitWireString);
        }

        int nextNumber = Channel + count;
        string nextWireString = IsHex ? $"{Device}{nextNumber:X}" : $"{Device}{nextNumber}";
        return new KeyenceHostLinkAddress($"{Original}+{count}", Device, nextNumber, 0, IsBit, IsHex, nextWireString);
    }
}
