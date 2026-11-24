using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 컴포넌트 주소 값 객체입니다. (사양서 §1.2)</summary>
public sealed class FatekAddress
{
    private const int FourDigitPadding = 4;
    private const int FiveDigitPadding = 5;
    private const int MaxFourDigitNumber = 9999;
    private const int MaxFiveDigitNumber = 65535;
    private const int BitsPerWordGroup = 16;
    private const int BitsPerDWordGroup = 32;
    private const int RegistersPerDWord = 2;
    private const int ThreeCharPrefixLength = 3;
    private const int TwoCharPrefixLength = 2;
    private const int SingleCharPrefixLength = 1;
    private const char WordGroupPrefixChar = 'W';
    private const char DoubleWordPrefixChar = 'D';

    private FatekAddress(
        string originalAddress,
        string device,
        int index,
        bool isBit,
        bool isWordGroup,
        bool is32Bit,
        int digitPadding,
        string wireString)
    {
        OriginalAddress = originalAddress;
        Device = device;
        Index = index;
        IsBit = isBit;
        IsWordGroup = isWordGroup;
        Is32Bit = is32Bit;
        DigitPadding = digitPadding;
        WireString = wireString;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string OriginalAddress { get; }

    /// <summary>컴포넌트 기호입니다. (예: "X", "Y", "M", "D", "R", "RT", "WM", "DR", "DWX" 등)</summary>
    public string Device { get; }

    /// <summary>0부터 시작하는 주소 번호입니다.</summary>
    public int Index { get; }

    /// <summary>비트 접점 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>이산 접점을 워드/더블워드로 묶은 그룹(WX~WC, DWX~DWC)인지 여부입니다.</summary>
    public bool IsWordGroup { get; }

    /// <summary>32비트(4바이트) 레지스터(DR, DD, DF, DRT, DRC, DW*) 여부입니다.</summary>
    public bool Is32Bit { get; }

    /// <summary>FATEK 통신 프레임 본문에 실리는 5/6/7/8문자 포맷 주소입니다.</summary>
    public string WireString { get; }

    private int DigitPadding { get; }

    /// <summary>주소 문자열을 해석하여 <see cref="FatekAddress"/> 인스턴스를 반환합니다.</summary>
    public static FatekAddress Parse(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!TryParse(address, out FatekAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 해석을 시도합니다.</summary>
    public static bool TryParse(string? address, [NotNullWhen(true)] out FatekAddress? result)
    {
        return TryParse(address, out result, out _);
    }

    /// <summary>오프셋만큼 증가된 새로운 <see cref="FatekAddress"/>를 반환합니다.</summary>
    public FatekAddress Offset(int elementOffset)
    {
        if (elementOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elementOffset), elementOffset, "오프셋은 0 이상이어야 합니다.");
        }

        int stride = GetElementStride();
        long nextIndexLong = (long)Index + ((long)elementOffset * stride);
        int maxAddressNumber = DigitPadding == FourDigitPadding ? MaxFourDigitNumber : MaxFiveDigitNumber;
        if (nextIndexLong > maxAddressNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elementOffset),
                elementOffset,
                $"이동된 FATEK 주소({Device}{nextIndexLong})가 최대 범위(0~{maxAddressNumber})를 초과합니다.");
        }

        int nextIndex = (int)nextIndexLong;
        string wireString = FormatWireString(Device, nextIndex, DigitPadding);
        return new FatekAddress($"{Device}{nextIndex}", Device, nextIndex, IsBit, IsWordGroup, Is32Bit, DigitPadding, wireString);
    }

    internal void ValidateContiguousRange(long unitCount, string paramName)
    {
        if (unitCount <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, unitCount, "요청 수량은 1 이상이어야 합니다.");
        }

        int stride = GetElementStride();
        long endBaseIndex = (long)Index + ((unitCount - 1) * stride);
        int maxAddressNumber = DigitPadding == FourDigitPadding ? MaxFourDigitNumber : MaxFiveDigitNumber;
        if (endBaseIndex > maxAddressNumber)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                unitCount,
                $"요청 범위가 FATEK {Device} 최대 주소(0~{maxAddressNumber})를 초과합니다.");
        }
    }

    private int GetElementStride()
    {
        if (IsWordGroup)
        {
            return Is32Bit ? BitsPerDWordGroup : BitsPerWordGroup;
        }

        if (Is32Bit && !string.Equals(Device, "DRC", StringComparison.Ordinal))
        {
            return RegistersPerDWord;
        }

        return 1;
    }

    private static bool TryParse(
        string? address,
        [NotNullWhen(true)] out FatekAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            errorMessage = "주소 문자열이 비어 있습니다.";
            return false;
        }

        string trimmed = address!.Trim().ToUpperInvariant();

        if (trimmed.StartsWith("DRC", StringComparison.Ordinal) || trimmed.StartsWith("DRT", StringComparison.Ordinal))
        {
            string threeCharPrefix = trimmed.Substring(0, ThreeCharPrefixLength);
            return TryParseCustom(trimmed, threeCharPrefix, ThreeCharPrefixLength, FourDigitPadding, isBit: false, isWordGroup: false, is32Bit: true, out result, out errorMessage);
        }

        if (trimmed.Length >= ThreeCharPrefixLength &&
            trimmed[0] == DoubleWordPrefixChar &&
            trimmed[1] == WordGroupPrefixChar &&
            IsDiscretePrefix(trimmed[2]))
        {
            string doubleWordGroupPrefix = trimmed.Substring(0, ThreeCharPrefixLength);
            return TryParseCustom(trimmed, doubleWordGroupPrefix, ThreeCharPrefixLength, FourDigitPadding, isBit: false, isWordGroup: true, is32Bit: true, out result, out errorMessage);
        }

        if (trimmed.StartsWith("DR", StringComparison.Ordinal) ||
            trimmed.StartsWith("DD", StringComparison.Ordinal) ||
            trimmed.StartsWith("DF", StringComparison.Ordinal))
        {
            string doubleWordPrefix = trimmed.Substring(0, TwoCharPrefixLength);
            return TryParseCustom(trimmed, doubleWordPrefix, TwoCharPrefixLength, FiveDigitPadding, isBit: false, isWordGroup: false, is32Bit: true, out result, out errorMessage);
        }

        if (trimmed.StartsWith("RT", StringComparison.Ordinal) || trimmed.StartsWith("RC", StringComparison.Ordinal))
        {
            string twoCharPrefix = trimmed.Substring(0, TwoCharPrefixLength);
            return TryParseCustom(trimmed, twoCharPrefix, TwoCharPrefixLength, FourDigitPadding, isBit: false, isWordGroup: false, is32Bit: false, out result, out errorMessage);
        }

        if (trimmed.Length >= TwoCharPrefixLength && trimmed[0] == WordGroupPrefixChar && IsDiscretePrefix(trimmed[1]))
        {
            string wordGroupPrefix = trimmed.Substring(0, TwoCharPrefixLength);
            return TryParseCustom(trimmed, wordGroupPrefix, TwoCharPrefixLength, FourDigitPadding, isBit: false, isWordGroup: true, is32Bit: false, out result, out errorMessage);
        }

        char firstChar = trimmed[0];
        if (IsDiscretePrefix(firstChar))
        {
            return TryParseCustom(trimmed, firstChar.ToString(), SingleCharPrefixLength, FourDigitPadding, isBit: true, isWordGroup: false, is32Bit: false, out result, out errorMessage);
        }

        if (firstChar is 'R' or 'D' or 'F')
        {
            return TryParseCustom(trimmed, firstChar.ToString(), SingleCharPrefixLength, FiveDigitPadding, isBit: false, isWordGroup: false, is32Bit: false, out result, out errorMessage);
        }

        errorMessage = $"지원하지 않는 FATEK 디바이스 접두사입니다: {address}";
        return false;
    }

    private static bool TryParseCustom(
        string original,
        string devicePrefix,
        int prefixLength,
        int padDigits,
        bool isBit,
        bool isWordGroup,
        bool is32Bit,
        [NotNullWhen(true)] out FatekAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string numberText = original.Substring(prefixLength);
        int maxAddressNumber = padDigits == FourDigitPadding ? MaxFourDigitNumber : MaxFiveDigitNumber;
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber)
            || parsedNumber < 0
            || parsedNumber > maxAddressNumber)
        {
            errorMessage = $"유효하지 않은 FATEK 주소 번호입니다: {original}";
            return false;
        }

        string wireString = FormatWireString(devicePrefix, parsedNumber, padDigits);
        result = new FatekAddress(original, devicePrefix, parsedNumber, isBit, isWordGroup, is32Bit, padDigits, wireString);
        return true;
    }

    private static string FormatWireString(string devicePrefix, int number, int padDigits)
    {
        string formatSpecifier = padDigits == FourDigitPadding ? "D4" : "D5";
        return $"{devicePrefix}{number.ToString(formatSpecifier, CultureInfo.InvariantCulture)}";
    }

    private static bool IsDiscretePrefix(char candidate) =>
        candidate is 'X' or 'Y' or 'M' or 'S' or 'T' or 'C';
}
