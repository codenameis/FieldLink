using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Panasonic;

/// <summary>Panasonic MEWTOCOL-COM 주소 표현입니다. (사양서 §1.2)</summary>
public sealed class MewtocolAddress
{
    private const int BitsPerWord = 16;
    private const int MaxBitOffset = BitsPerWord - 1;
    private const int MaxTimerCounterNumber = 999;
    private const int MaxContactWordNumber = 999;
    private const int MaxWordRegisterNumber = 99999;
    private const int TwoCharPrefixLength = 2;
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

    /// <summary>디바이스 접두사입니다. (예: "X", "Y", "R", "L", "T", "C", "D", "F")</summary>
    public string Device { get; }

    /// <summary>워드 번호 또는 접점 인덱스 번호입니다.</summary>
    public int Number { get; }

    /// <summary>접점 비트 오프셋(0~15)입니다.</summary>
    public byte BitOffset { get; }

    /// <summary>비트 접점 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>MEWTOCOL 와이어 주소 문자열입니다. (접점: "X0000", 워드 영역: "D")</summary>
    public string WireString { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    /// <param name="address">파싱할 주소 문자열입니다.</param>
    /// <returns>해석된 <see cref="MewtocolAddress"/> 인스턴스입니다.</returns>
    public static MewtocolAddress Parse(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!TryParse(address, out MewtocolAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    /// <param name="address">파싱할 주소 문자열입니다.</param>
    /// <param name="result">파싱에 성공한 경우 결과 주소 객체입니다.</param>
    /// <param name="errorMessage">파싱에 실패한 경우 오류 사유입니다.</param>
    /// <returns>파싱 성공 여부입니다.</returns>
    public static bool TryParse(
        string? address,
        [NotNullWhen(true)] out MewtocolAddress? result,
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
        foreach ((string inputPrefix, string wireDevice) in MultiCharWordPrefixes)
        {
            if (trimmed.StartsWith(inputPrefix, StringComparison.Ordinal))
            {
                string remaining = trimmed.Substring(TwoCharPrefixLength);
                if (remaining.Length == 0)
                {
                    errorMessage = $"주소 번호가 없습니다: {address}";
                    return false;
                }

                return TryParseWord(trimmed, wireDevice, remaining, out result, out errorMessage);
            }
        }

        char firstChar = trimmed[0];
        string numberText = trimmed.Substring(SingleCharacterLength);
        if (numberText.Length == 0)
        {
            errorMessage = $"주소 번호가 없습니다: {address}";
            return false;
        }

        if (firstChar is 'D' or 'F')
        {
            return TryParseWord(trimmed, firstChar.ToString(), numberText, out result, out errorMessage);
        }

        if (firstChar is 'X' or 'Y' or 'R' or 'L' or 'T' or 'C')
        {
            return TryParseContact(trimmed, firstChar, numberText, out result, out errorMessage);
        }

        errorMessage = $"지원하지 않는 MEWTOCOL 디바이스입니다: {address}";
        return false;
    }

    /// <summary>지정한 오프셋만큼 이동한 새 <see cref="MewtocolAddress"/>를 반환합니다.</summary>
    /// <param name="elementOffset">이동할 오프셋(비트 주소면 비트 수, 워드 주소면 워드 수)입니다.</param>
    /// <returns>오프셋이 적용된 새 주소입니다.</returns>
    public MewtocolAddress Offset(int elementOffset)
    {
        if (elementOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elementOffset), elementOffset, "오프셋은 0 이상이어야 합니다.");
        }

        if (IsBit)
        {
            return OffsetBitAddress(elementOffset);
        }

        int nextWordNumber = checked(Number + elementOffset);
        return new MewtocolAddress($"{Original}+{elementOffset}", Device, nextWordNumber, 0, isBit: false, Device);
    }

    private MewtocolAddress OffsetBitAddress(int bitOffsetDelta)
    {
        if (Device is "T" or "C")
        {
            int nextTimerCounterNumber = checked(Number + bitOffsetDelta);
            string timerCounterWire = $"{Device}0{nextTimerCounterNumber:D3}";
            return new MewtocolAddress($"{Original}+{bitOffsetDelta}", Device, nextTimerCounterNumber, 0, isBit: true, timerCounterWire);
        }

        int totalBits = checked((Number * BitsPerWord) + BitOffset + bitOffsetDelta);
        int nextWordNumber = totalBits / BitsPerWord;
        byte nextBitOffset = (byte)(totalBits % BitsPerWord);
        string contactWire = $"{Device}{nextWordNumber:D3}{nextBitOffset:X1}";
        return new MewtocolAddress($"{Original}+{bitOffsetDelta}", Device, nextWordNumber, nextBitOffset, isBit: true, contactWire);
    }

    private static bool TryParseContact(
        string original,
        char deviceChar,
        string numberText,
        [NotNullWhen(true)] out MewtocolAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (deviceChar is 'T' or 'C')
        {
            return TryParseTimerOrCounterContact(original, deviceChar, numberText, out result, out errorMessage);
        }

        int dotIndex = numberText.IndexOf(BitSeparator);
        if (dotIndex >= 0)
        {
            return TryParseDotContact(original, deviceChar, numberText, dotIndex, out result, out errorMessage);
        }

        if (numberText.Length == SingleCharacterLength)
        {
            return TryParseSingleDigitContact(original, deviceChar, numberText, out result, out errorMessage);
        }

        return TryParseMixedRadixContact(original, deviceChar, numberText, out result, out errorMessage);
    }

    private static bool TryParseTimerOrCounterContact(
        string original,
        char deviceChar,
        string numberText,
        [NotNullWhen(true)] out MewtocolAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int timerCounterNumber)
            || timerCounterNumber > MaxTimerCounterNumber)
        {
            errorMessage = $"유효하지 않은 타이머/카운터 접점 번호 (0~{MaxTimerCounterNumber}): {original}";
            return false;
        }

        string wireString = $"{deviceChar}0{timerCounterNumber:D3}";
        result = new MewtocolAddress(original, deviceChar.ToString(), timerCounterNumber, 0, isBit: true, wireString);
        return true;
    }

    private static bool TryParseSingleDigitContact(
        string original,
        char deviceChar,
        string numberText,
        [NotNullWhen(true)] out MewtocolAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (!byte.TryParse(numberText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte bitOffset))
        {
            errorMessage = $"16진 비트 번호 파싱 실패: {original}";
            return false;
        }

        string wireString = $"{deviceChar}000{bitOffset:X1}";
        result = new MewtocolAddress(original, deviceChar.ToString(), 0, bitOffset, isBit: true, wireString);
        return true;
    }

    private static bool TryParseMixedRadixContact(
        string original,
        char deviceChar,
        string numberText,
        [NotNullWhen(true)] out MewtocolAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string wordPart = numberText.Substring(0, numberText.Length - SingleCharacterLength);
        string bitPart = numberText.Substring(numberText.Length - SingleCharacterLength);

        bool isValidWord = int.TryParse(wordPart, NumberStyles.None, CultureInfo.InvariantCulture, out int wordNumber)
            && wordNumber <= MaxContactWordNumber;
        bool isValidBit = byte.TryParse(bitPart, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte bitOffset);
        if (!isValidWord || !isValidBit)
        {
            errorMessage = $"MEWTOCOL 접점 주소 파싱 실패: {original}";
            return false;
        }

        string wireString = $"{deviceChar}{wordNumber:D3}{bitOffset:X1}";
        result = new MewtocolAddress(original, deviceChar.ToString(), wordNumber, bitOffset, isBit: true, wireString);
        return true;
    }

    private static bool TryParseDotContact(
        string original,
        char deviceChar,
        string numberText,
        int dotIndex,
        [NotNullWhen(true)] out MewtocolAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string wordPart = numberText.Substring(0, dotIndex);
        string bitPart = numberText.Substring(dotIndex + 1);

        bool isValidWord = int.TryParse(wordPart, NumberStyles.None, CultureInfo.InvariantCulture, out int wordNumber)
            && wordNumber <= MaxContactWordNumber;
        bool isValidBit = TryParseBitOffset(bitPart, out byte bitOffset);
        if (!isValidWord || !isValidBit)
        {
            errorMessage = $"MEWTOCOL 접점 파싱 실패: {original}";
            return false;
        }

        string wireString = $"{deviceChar}{wordNumber:D3}{bitOffset:X1}";
        result = new MewtocolAddress(original, deviceChar.ToString(), wordNumber, bitOffset, isBit: true, wireString);
        return true;
    }

    private static bool TryParseBitOffset(string bitPart, out byte bitOffset)
    {
        if (int.TryParse(bitPart, NumberStyles.None, CultureInfo.InvariantCulture, out int decimalBit)
            && decimalBit <= MaxBitOffset)
        {
            bitOffset = (byte)decimalBit;
            return true;
        }

        if (bitPart.Length == SingleCharacterLength
            && byte.TryParse(bitPart, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte hexBit)
            && hexBit <= MaxBitOffset)
        {
            bitOffset = hexBit;
            return true;
        }

        bitOffset = 0;
        return false;
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
            || parsedNumber > MaxWordRegisterNumber)
        {
            errorMessage = $"유효하지 않은 MEWTOCOL 워드 주소: {original}";
            return false;
        }

        result = new MewtocolAddress(original, devicePrefix, parsedNumber, 0, isBit: false, devicePrefix);
        return true;
    }
}
