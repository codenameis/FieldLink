using System;
using System.Diagnostics.CodeAnalysis;

namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 프로토콜의 디바이스 주소 표현입니다. (사양서 Part A §A.1)</summary>
public sealed class KeyenceHostLinkAddress
{
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
        devicePrefix = string.Empty;
        isBit = false;
        isHex = false;
        numberText = string.Empty;

        string[] twoCharPrefixes = ["CTH", "CTC", "MR", "LR", "CR", "VB", "DM", "EM", "FM", "ZF", "TM", "CM", "VM", "AT", "TC", "TS", "CC", "CS"];
        foreach (string d in twoCharPrefixes)
        {
            if (trimmed.StartsWith(d, StringComparison.Ordinal))
            {
                devicePrefix = d;
                numberText = trimmed.Substring(d.Length);
                isBit = d is "MR" or "LR" or "CR" or "VB";
                isHex = d is "VB";
                return true;
            }
        }

        string[] dev1 = ["R", "B", "W", "Z", "T", "C"];
        foreach (string d in dev1)
        {
            if (trimmed.StartsWith(d, StringComparison.Ordinal))
            {
                devicePrefix = d;
                numberText = trimmed.Substring(1);
                isBit = d is "R" or "B";
                isHex = d is "B" or "W";
                return true;
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

        int dot = numberText.IndexOf('.');
        if (dot >= 0)
        {
            return TryParseDotBit(original, devicePrefix, isHex, numberText, dot, out result, out errorMessage);
        }

        if (isHex)
        {
            if (!int.TryParse(numberText, System.Globalization.NumberStyles.HexNumber, null, out int hexVal))
            {
                errorMessage = $"16진 비트 주소 파싱 실패: {original}";
                return false;
            }

            string wire = $"{devicePrefix}{hexVal:X}";
            result = new KeyenceHostLinkAddress(original, devicePrefix, hexVal, 0, isBit: true, isHex: true, wire);
            return true;
        }

        if (!int.TryParse(numberText, out int decVal))
        {
            errorMessage = $"10진 비트 주소 파싱 실패: {original}";
            return false;
        }

        int ch = decVal < 16 ? 0 : decVal / 100;
        byte bit = (byte)(decVal < 16 ? decVal : decVal % 100);
        string wireDec = decVal < 16 ? $"{devicePrefix}{decVal}" : $"{devicePrefix}{ch}{bit:D2}";
        result = new KeyenceHostLinkAddress(original, devicePrefix, ch, bit, isBit: true, isHex: false, wireDec);
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
            if (!int.TryParse(chStr, System.Globalization.NumberStyles.HexNumber, null, out ch))
            {
                errorMessage = $"16진 채널 번호 오류: {original}";
                return false;
            }
        }
        else if (!int.TryParse(chStr, out ch))
        {
            errorMessage = $"10진 채널 번호 오류: {original}";
            return false;
        }

        if (!byte.TryParse(bitStr, out byte bit) || bit > 15)
        {
            errorMessage = $"비트 오프셋(0~15) 오류: {original}";
            return false;
        }

        string wire = isHex ? $"{devicePrefix}{ch:X}.{bit}" : $"{devicePrefix}{ch}{bit:D2}";
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
            if (!int.TryParse(numberText, System.Globalization.NumberStyles.HexNumber, null, out parsedNumber))
            {
                errorMessage = $"16진 워드 주소 파싱 실패: {original}";
                return false;
            }
        }
        else if (!int.TryParse(numberText, out parsedNumber))
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
        if (IsBit && !IsHex)
        {
            int totalBit = (Channel * 16) + Bit + count;
            int newCh = totalBit / 16;
            byte newBit = (byte)(totalBit % 16);
            string wire = totalBit < 16 ? $"{Device}{totalBit}" : $"{Device}{newCh}{newBit:D2}";
            return new KeyenceHostLinkAddress($"{Original}+{count}", Device, newCh, newBit, true, false, wire);
        }

        int newNum = Channel + count;
        string newWire = IsHex ? $"{Device}{newNum:X}" : $"{Device}{newNum}";
        return new KeyenceHostLinkAddress($"{Original}+{count}", Device, newNum, 0, IsBit, IsHex, newWire);
    }
}
