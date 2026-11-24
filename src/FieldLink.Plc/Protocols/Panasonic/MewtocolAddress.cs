using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 프로토콜의 주소 표현입니다. (사양서 §1.3, §1.4)</summary>
public sealed class MewtocolAddress
{
    private const int BitsPerWord = 16;
    private const int MaxBitOffset = BitsPerWord - 1;
    private const int MaxTimerCounterNumber = 999;
    private const int MaxContactWordNumber = 999;
    private const int MaxWordNumber = 99999;
    private const int SingleCharacterLength = 1;
    private const char BitSeparator = '.';

    private static readonly (string InputPrefix, string WireDevice)[] MultiCharWordPrefixes =
    [
        ("DT", "D"),
        ("LD", "L"),
        ("FL", "F"),
    ];

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
            errorMessage = "주소 문자열은 필수입니다.";
            return false;
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        foreach ((string inputPrefix, string wireDevice) in MultiCharWordPrefixes)
        {
            if (trimmed.StartsWith(inputPrefix, StringComparison.Ordinal))
            {
                return TryParseWord(trimmed, wireDevice, trimmed.Substring(inputPrefix.Length), out result, out errorMessage);
            }
        }

        char p1 = trimmed[0];
        string numberText = trimmed.Substring(SingleCharacterLength);
        if (numberText.Length == 0)
        {
            errorMessage = $"주소 번호가 없습니다: {address}";
            return false;
        }

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
            if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int tcNum)
                || tcNum < 0
                || tcNum > MaxTimerCounterNumber)
            {
                errorMessage = $"유효하지 않은 타이머/카운터 접점 번호 (0~{MaxTimerCounterNumber}): {original}";
                return false;
            }

            string wireTc = $"{p1}0{tcNum:D3}";
            result = new MewtocolAddress(original, p1.ToString(), tcNum, 0, isBit: true, wireTc);
            return true;
        }

        int dot = numberText.IndexOf(BitSeparator);
        if (dot >= 0)
        {
            return TryParseDotContact(original, p1, numberText, dot, out result, out errorMessage);
        }

        if (numberText.Length == SingleCharacterLength)
        {
            if (!byte.TryParse(numberText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte bSingle))
            {
                errorMessage = $"16진 비트 번호 파싱 실패: {original}";
                return false;
            }

            string wireSingle = $"{p1}000{bSingle:X1}";
            result = new MewtocolAddress(original, p1.ToString(), 0, bSingle, isBit: true, wireSingle);
            return true;
        }

        string wordPart = numberText.Substring(0, numberText.Length - SingleCharacterLength);
        string bitPart = numberText.Substring(numberText.Length - SingleCharacterLength);

        if (!int.TryParse(wordPart, NumberStyles.None, CultureInfo.InvariantCulture, out int wNum)
            || wNum > MaxContactWordNumber
            || !byte.TryParse(bitPart, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte b))
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

        if (!int.TryParse(wStr, NumberStyles.None, CultureInfo.InvariantCulture, out int w)
            || w > MaxContactWordNumber
            || !byte.TryParse(bStr, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte b)
            || b > MaxBitOffset)
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

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber)
            || parsedNumber < 0
            || parsedNumber > MaxWordNumber)
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
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        if (IsBit)
        {
            if (Device is "T" or "C")
            {
                int newNum = checked(Number + count);
                string wire = $"{Device}0{newNum:D3}";
                return new MewtocolAddress($"{Original}+{count}", Device, newNum, 0, isBit: true, wire);
            }

            int totalBits = checked((Number * BitsPerWord) + BitOffset + count);
            int newWord = totalBits / BitsPerWord;
            byte newBit = (byte)(totalBits % BitsPerWord);
            string contactWire = $"{Device}{newWord:D3}{newBit:X1}";
            return new MewtocolAddress($"{Original}+{count}", Device, newWord, newBit, isBit: true, contactWire);
        }

        int nextWord = checked(Number + count);
        return new MewtocolAddress($"{Original}+{count}", Device, nextWord, 0, isBit: false, Device);
    }
}
