using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB 프로토콜의 주소 표현입니다. (사양서 Part A §A.1.2)</summary>
public sealed class FujiElectricSpbAddress
{
    private const int BitsPerWord = 16;
    private const int MaxBitOffset = 15;
    private const int BitsPerByte = 8;
    private const int BytesPerWord = 2;
    private const int SwappedDigitLength = 4;
    private const int HalfDigitLength = 2;

    private static readonly string[] TwoCharPrefixes = ["TC", "CC", "TN", "CN"];

    private FujiElectricSpbAddress(
        string original,
        string areaCode,
        ushort wordAddress,
        byte bitOffset,
        bool isBit,
        string wireWordAddress)
    {
        Original = original;
        AreaCode = areaCode;
        WordAddress = wordAddress;
        BitOffset = bitOffset;
        IsBit = isBit;
        WireWordAddress = wireWordAddress;
    }

    /// <summary>입력된 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>2자리 16진 텍스트 영역 코드입니다. (예: D는 "0C", M은 "02")</summary>
    public string AreaCode { get; }

    /// <summary>16비트 워드 번호입니다.</summary>
    public ushort WordAddress { get; }

    /// <summary>워드 내 비트 오프셋(0~15)입니다.</summary>
    public byte BitOffset { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>워드 접근 시 6문자 주소 문자열입니다. (영역코드 2문자 + 자리교차 4문자)</summary>
    public string WireWordAddress { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    /// <param name="address">파싱할 Fuji SPB 주소 문자열입니다.</param>
    /// <returns>파싱된 <see cref="FujiElectricSpbAddress"/> 인스턴스입니다.</returns>
    public static FujiElectricSpbAddress Parse(string address)
    {
        if (!TryParse(address, out FujiElectricSpbAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    /// <param name="address">파싱할 Fuji SPB 주소 문자열입니다.</param>
    /// <param name="result">파싱에 성공한 주소 객체입니다.</param>
    /// <param name="errorMessage">파싱 실패 시 오류 설명입니다.</param>
    /// <returns>파싱 성공 여부입니다.</returns>
    public static bool TryParse(
        string? address,
        [NotNullWhen(true)] out FujiElectricSpbAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (!TryExtractPrefix(trimmed, out string areaCode, out bool isBitArea, out string numberText))
        {
            errorMessage = $"지원하지 않는 Fuji SPB 주소 접두사입니다: {address}";
            return false;
        }

        return TryParseNumber(trimmed, areaCode, isBitArea, numberText, out result, out errorMessage);
    }

    /// <summary>10진 <see cref="ushort"/> 값을 4자리로 만들고 상위 2자리와 하위 2자리를 맞바꿉니다. (예: 100 → "0100" → "0001")</summary>
    /// <param name="value">변환할 10진 워드/바이트 수치입니다.</param>
    /// <returns>상·하위 2자리가 교차된 4자리 10진 문자열입니다.</returns>
    public static string FormatSwapped4(ushort value)
    {
        string fourDigitDecimal = value.ToString("D4", CultureInfo.InvariantCulture);
        return fourDigitDecimal.Substring(HalfDigitLength, HalfDigitLength)
            + fourDigitDecimal.Substring(0, HalfDigitLength);
    }

    /// <summary>상·하위 교차된 4자리 10진 문자열을 <see cref="ushort"/>로 복원합니다. (예: "0001" → "0100" → 100)</summary>
    /// <param name="swapped4">자리교차된 4자리 문자열입니다.</param>
    /// <returns>복원된 수치입니다.</returns>
    public static ushort Unswap4(string swapped4)
    {
        if (swapped4 is null || swapped4.Length != SwappedDigitLength)
        {
            throw new ArgumentException("4자리 문자열이어야 합니다.", nameof(swapped4));
        }

        string fourDigitDecimal = swapped4.Substring(HalfDigitLength, HalfDigitLength)
            + swapped4.Substring(0, HalfDigitLength);
        return ushort.Parse(fourDigitDecimal, CultureInfo.InvariantCulture);
    }

    /// <summary>단일 비트 쓰기용 8문자 주소를 생성합니다. (영역코드 2문자 + 자리교차 4문자 바이트인덱스 + 바이트내비트 2자리)</summary>
    /// <returns>8문자 ASCII 비트 쓰기 주소입니다.</returns>
    public string BuildBitWriteAddress()
    {
        int byteIndex = (WordAddress * BytesPerWord) + (BitOffset / BitsPerByte);
        int bitInByte = BitOffset % BitsPerByte;
        string swappedByteIndex = FormatSwapped4((ushort)byteIndex);
        return $"{AreaCode}{swappedByteIndex}{bitInByte:D2}";
    }

    /// <summary>오프셋만큼 워드 또는 비트 주소를 이동한 새 인스턴스를 반환합니다.</summary>
    /// <param name="count">이동할 비트 수(<see cref="IsBit"/>가 true일 때) 또는 워드 수입니다.</param>
    /// <returns>오프셋이 반영된 새 <see cref="FujiElectricSpbAddress"/> 인스턴스입니다.</returns>
    public FujiElectricSpbAddress Offset(int count)
    {
        if (IsBit)
        {
            int totalBits = (WordAddress * BitsPerWord) + BitOffset + count;
            ushort newWordAddress = (ushort)(totalBits / BitsPerWord);
            byte newBitOffset = (byte)(totalBits % BitsPerWord);
            string wireWordAddress = $"{AreaCode}{FormatSwapped4(newWordAddress)}";
            return new FujiElectricSpbAddress(
                $"{AreaCode}.{totalBits}",
                AreaCode,
                newWordAddress,
                newBitOffset,
                isBit: true,
                wireWordAddress);
        }

        ushort nextWordAddress = (ushort)(WordAddress + count);
        string nextWireWordAddress = $"{AreaCode}{FormatSwapped4(nextWordAddress)}";
        return new FujiElectricSpbAddress(
            $"{AreaCode}{nextWordAddress}",
            AreaCode,
            nextWordAddress,
            bitOffset: 0,
            isBit: false,
            nextWireWordAddress);
    }

    internal FujiElectricSpbAddress ToWordAddress(ushort wordAddress)
    {
        string wireWordAddress = $"{AreaCode}{FormatSwapped4(wordAddress)}";
        return new FujiElectricSpbAddress(
            Original,
            AreaCode,
            wordAddress,
            bitOffset: 0,
            isBit: false,
            wireWordAddress);
    }

    internal FujiElectricSpbAddress ToBitAddress()
    {
        if (IsBit)
        {
            return this;
        }

        return new FujiElectricSpbAddress(
            Original,
            AreaCode,
            WordAddress,
            BitOffset,
            isBit: true,
            WireWordAddress);
    }

    private static bool TryExtractPrefix(
        string trimmed,
        out string areaCode,
        out bool isBitArea,
        out string numberText)
    {
        foreach (string twoCharPrefix in TwoCharPrefixes)
        {
            if (trimmed.StartsWith(twoCharPrefix, StringComparison.Ordinal))
            {
                numberText = trimmed.Substring(HalfDigitLength);
                areaCode = MapTwoCharAreaCode(twoCharPrefix);
                isBitArea = twoCharPrefix is "TC" or "CC";
                return true;
            }
        }

        char firstChar = trimmed[0];
        numberText = trimmed.Substring(1);
        isBitArea = firstChar is 'Y' or 'X' or 'M' or 'L';
        areaCode = MapSingleCharAreaCode(firstChar);
        return !string.IsNullOrEmpty(areaCode);
    }

    private static string MapTwoCharAreaCode(string twoCharPrefix) => twoCharPrefix switch
    {
        "TC" => "04",
        "CC" => "05",
        "TN" => "0A",
        "CN" => "0B",
        _ => string.Empty,
    };

    private static string MapSingleCharAreaCode(char firstChar) => firstChar switch
    {
        'Y' => "00",
        'X' => "01",
        'M' => "02",
        'L' => "03",
        'D' => "0C",
        'R' => "0D",
        'W' => "0E",
        _ => string.Empty,
    };

    private static bool TryParseNumber(
        string original,
        string areaCode,
        bool isBitArea,
        string numberText,
        [NotNullWhen(true)] out FujiElectricSpbAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        int dotIndex = numberText.IndexOf('.');
        if (dotIndex >= 0)
        {
            return TryParseDotAddress(original, areaCode, numberText, dotIndex, out result, out errorMessage);
        }

        if (!ushort.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort parsedNumber))
        {
            errorMessage = $"Fuji SPB 주소 번호가 올바르지 않습니다: {original}";
            return false;
        }

        ushort wordAddress = isBitArea ? (ushort)(parsedNumber / BitsPerWord) : parsedNumber;
        byte bitOffset = isBitArea ? (byte)(parsedNumber % BitsPerWord) : (byte)0;
        string wireWordAddress = $"{areaCode}{FormatSwapped4(wordAddress)}";
        result = new FujiElectricSpbAddress(original, areaCode, wordAddress, bitOffset, isBitArea, wireWordAddress);
        return true;
    }

    private static bool TryParseDotAddress(
        string original,
        string areaCode,
        string numberText,
        int dotIndex,
        [NotNullWhen(true)] out FujiElectricSpbAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string wordPart = numberText.Substring(0, dotIndex);
        string bitPart = numberText.Substring(dotIndex + 1);

        if (!ushort.TryParse(wordPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort wordAddress))
        {
            errorMessage = $"워드 주소 파싱 실패: {original}";
            return false;
        }

        if (!byte.TryParse(bitPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte bitOffset)
            || bitOffset > MaxBitOffset)
        {
            errorMessage = $"비트 오프셋 파싱 실패 (0~F): {original}";
            return false;
        }

        string wireWordAddress = $"{areaCode}{FormatSwapped4(wordAddress)}";
        result = new FujiElectricSpbAddress(original, areaCode, wordAddress, bitOffset, isBit: true, wireWordAddress);
        return true;
    }
}
