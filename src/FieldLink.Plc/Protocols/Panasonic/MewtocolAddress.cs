using System;
using System.Diagnostics.CodeAnalysis;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 프로토콜의 주소 표현입니다. (사양서 §1.3, §1.4)</summary>
public sealed class MewtocolAddress
{
    private MewtocolAddress(string original, string device, int number, byte bitOffset, bool isBit, string wireString)
    {
        Original = original;
        Device = device;
        Number = number;
        BitOffset = bitOffset;
        IsBit = isBit;
        WireString = wireString;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>디바이스 접두사입니다. (예: "X", "Y", "R", "L", "T", "C", "D", "L", "F")</summary>
    public string Device { get; }

    /// <summary>워드 번호 또는 접점 인덱스 번호입니다.</summary>
    public int Number { get; }

    /// <summary>워드 내 비트 오프셋 (0~15)입니다.</summary>
    public byte BitOffset { get; }

    /// <summary>비트 접점 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>MEWTOCOL 와이어 표현 문자열입니다. (코일 5문자: "R0100", 데이터영역 1문자: "D")</summary>
    public string WireString { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static MewtocolAddress Parse(string address)
    {
        if (!TryParse(address, out MewtocolAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        [NotNullWhen(true)] out MewtocolAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (trimmed.StartsWith("DT", StringComparison.Ordinal))
        {
            return TryParseWord(trimmed, "D", trimmed.Substring(2), out result, out errorMessage);
        }

        if (trimmed.StartsWith("LD", StringComparison.Ordinal))
        {
            return TryParseWord(trimmed, "L", trimmed.Substring(2), out result, out errorMessage);
        }

        if (trimmed.StartsWith("FL", StringComparison.Ordinal))
        {
            return TryParseWord(trimmed, "F", trimmed.Substring(2), out result, out errorMessage);
        }

        char p1 = trimmed[0];
        string numberText = trimmed.Substring(1);

        if (p1 is 'D' or 'F')
        {
            return TryParseWord(trimmed, p1.ToString(), numberText, out result, out errorMessage);
        }

        if (p1 is 'X' or 'Y' or 'R' or 'L' or 'T' or 'C')
        {
            return TryParseContact(trimmed, p1, numberText, out result, out errorMessage);
        }

        errorMessage = $"지원하지 않는 MEWTOCOL 디바이스입니다: {address}";
        return false;
    }

    private static bool TryParseContact(
        string original,
        char p1,
        string numberText,
        [NotNullWhen(true)] out MewtocolAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (p1 is 'T' or 'C')
        {
            if (!int.TryParse(numberText, out int tcNum) || tcNum < 0 || tcNum > 999)
            {
                errorMessage = $"유효하지 않은 타이머/카운터 접점 번호 (0~999): {original}";
                return false;
            }

            string wireTc = $"{p1}0{tcNum:D3}";
            result = new MewtocolAddress(original, p1.ToString(), tcNum, 0, isBit: true, wireTc);
            return true;
        }

        int dot = numberText.IndexOf('.');
        if (dot >= 0)
        {
            return TryParseDotContact(original, p1, numberText, dot, out result, out errorMessage);
        }

        if (numberText.Length == 1)
        {
            if (!byte.TryParse(numberText, System.Globalization.NumberStyles.HexNumber, null, out byte bSingle))
            {
                errorMessage = $"16진 비트 번호 파싱 실패: {original}";
                return false;
            }

            string wireSingle = $"{p1}000{bSingle:X1}";
            result = new MewtocolAddress(original, p1.ToString(), 0, bSingle, isBit: true, wireSingle);
            return true;
        }

        string wordPart = numberText.Substring(0, numberText.Length - 1);
        string bitPart = numberText.Substring(numberText.Length - 1);

        if (!int.TryParse(wordPart, out int wNum) || !byte.TryParse(bitPart, System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            errorMessage = $"MEWTOCOL 접점 주소 파싱 실패: {original}";
            return false;
        }

        string wire = $"{p1}{wNum:D3}{b:X1}";
        result = new MewtocolAddress(original, p1.ToString(), wNum, b, isBit: true, wire);
        return true;
    }

    private static bool TryParseDotContact(
        string original,
        char p1,
        string numberText,
        int dot,
        [NotNullWhen(true)] out MewtocolAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string wStr = numberText.Substring(0, dot);
        string bStr = numberText.Substring(dot + 1);

        if (!int.TryParse(wStr, out int w) || !byte.TryParse(bStr, System.Globalization.NumberStyles.HexNumber, null, out byte b) || b > 15)
        {
            errorMessage = $"MEWTOCOL 접점 파싱 실패: {original}";
            return false;
        }

        string wire = $"{p1}{w:D3}{b:X1}";
        result = new MewtocolAddress(original, p1.ToString(), w, b, isBit: true, wire);
        return true;
    }

    private static bool TryParseWord(
        string original,
        string devicePrefix,
        string numberText,
        [NotNullWhen(true)] out MewtocolAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (!int.TryParse(numberText, out int parsedNumber) || parsedNumber < 0 || parsedNumber > 99999)
        {
            errorMessage = $"유효하지 않은 MEWTOCOL 워드 주소: {original}";
            return false;
        }

        result = new MewtocolAddress(original, devicePrefix, parsedNumber, 0, isBit: false, devicePrefix);
        return true;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="MewtocolAddress"/>를 반환합니다.</summary>
    public MewtocolAddress Offset(int count)
    {
        if (IsBit)
        {
            if (Device is "T" or "C")
            {
                int newNum = Number + count;
                string wire = $"{Device}0{newNum:D3}";
                return new MewtocolAddress($"{Original}+{count}", Device, newNum, 0, true, wire);
            }
            else
            {
                int totalBits = (Number * 16) + BitOffset + count;
                int newWord = totalBits / 16;
                byte newBit = (byte)(totalBits % 16);
                string wire = $"{Device}{newWord:D3}{newBit:X1}";
                return new MewtocolAddress($"{Original}+{count}", Device, newWord, newBit, true, wire);
            }
        }

        int nextWord = Number + count;
        return new MewtocolAddress($"{Original}+{count}", Device, nextWord, 0, false, Device);
    }
}
