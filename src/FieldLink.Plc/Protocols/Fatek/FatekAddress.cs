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
    private const int ThreeCharPrefixLength = 3;
    private const int TwoCharPrefixLength = 2;
    private const int SingleCharPrefixLength = 1;
    private const char WordGroupPrefixChar = 'W';

    private FatekAddress(
        string originalAddress,
        string device,
        int index,
        bool isBit,
        bool isWordGroup,
        string wireString)
    {
        OriginalAddress = originalAddress;
        Device = device;
        Index = index;
        IsBit = isBit;
        IsWordGroup = isWordGroup;
        WireString = wireString;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string OriginalAddress { get; }

    /// <summary>컴포넌트 기호입니다. (예: "X", "Y", "M", "D", "R", "RT", "WM" 등)</summary>
    public string Device { get; }

    /// <summary>0부터 시작하는 주소 번호입니다.</summary>
    public int Index { get; }

    /// <summary>비트 접점 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>이산 접점을 16비트 워드로 묶은 그룹(WX~WC)인지 여부입니다.</summary>
    public bool IsWordGroup { get; }

    /// <summary>32비트(4바이트) 폭을 가지는 카운터 레지스터(DRC)인지 여부입니다.</summary>
    public bool Is32Bit => string.Equals(Device, "DRC", StringComparison.Ordinal);

    /// <summary>FATEK 통신 프레임 본문에 실리는 5/6/7문자 포맷 주소입니다.</summary>
    public string WireString { get; }

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

        if (trimmed.StartsWith("DRC", StringComparison.Ordinal))
        {
            return TryParseCustom(trimmed, "DRC", ThreeCharPrefixLength, FourDigitPadding, isBit: false, isWordGroup: false, out result, out errorMessage);
        }

        if (trimmed.StartsWith("RT", StringComparison.Ordinal) || trimmed.StartsWith("RC", StringComparison.Ordinal))
        {
            string twoCharPrefix = trimmed.Substring(0, TwoCharPrefixLength);
            return TryParseCustom(trimmed, twoCharPrefix, TwoCharPrefixLength, FourDigitPadding, isBit: false, isWordGroup: false, out result, out errorMessage);
        }

        if (trimmed.Length >= TwoCharPrefixLength && trimmed[0] == WordGroupPrefixChar && IsDiscretePrefix(trimmed[1]))
        {
            string wordGroupPrefix = trimmed.Substring(0, TwoCharPrefixLength);
            return TryParseCustom(trimmed, wordGroupPrefix, TwoCharPrefixLength, FourDigitPadding, isBit: false, isWordGroup: true, out result, out errorMessage);
        }

        char firstChar = trimmed[0];
        if (IsDiscretePrefix(firstChar))
        {
            return TryParseCustom(trimmed, firstChar.ToString(), SingleCharPrefixLength, FourDigitPadding, isBit: true, isWordGroup: false, out result, out errorMessage);
        }

        if (firstChar is 'R' or 'D' or 'F')
        {
            return TryParseCustom(trimmed, firstChar.ToString(), SingleCharPrefixLength, FiveDigitPadding, isBit: false, isWordGroup: false, out result, out errorMessage);
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

        string formatSpecifier = padDigits == FourDigitPadding ? "D4" : "D5";
        string wireString = $"{devicePrefix}{parsedNumber.ToString(formatSpecifier, CultureInfo.InvariantCulture)}";

        result = new FatekAddress(original, devicePrefix, parsedNumber, isBit, isWordGroup, wireString);
        return true;
    }

    private static bool IsDiscretePrefix(char candidate) =>
        candidate is 'X' or 'Y' or 'M' or 'S' or 'T' or 'C';

    /// <summary>오프셋만큼 증가된 새로운 <see cref="FatekAddress"/>를 반환합니다.</summary>
    public FatekAddress Offset(int elementOffset)
    {
        if (elementOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elementOffset), elementOffset, "오프셋은 0 이상이어야 합니다.");
        }

        int nextIndex = IsWordGroup
            ? checked(Index + (elementOffset * BitsPerWordGroup))
            : checked(Index + elementOffset);
        string newOriginal = $"{Device}{nextIndex}";
        return Parse(newOriginal);
    }
}
