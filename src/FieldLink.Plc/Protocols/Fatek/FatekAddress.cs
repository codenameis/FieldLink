using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 컴포넌트 주소 값 객체입니다. (사양서 §1.2)</summary>
public sealed class FatekAddress
{
    private const int ThreeCharPrefixLength = 3;
    private const int TwoCharPrefixLength = 2;
    private const int OneCharPrefixLength = 1;
    private const int ShortAddressDigits = 4;
    private const int WordAddressDigits = 5;
    private const int MaxFourDigitIndex = 9999;
    private const int MaxFiveDigitIndex = 65535;
    private const int WordGroupBitStride = 16;

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
            return TryParseCustom(trimmed, "DRC", ThreeCharPrefixLength, ShortAddressDigits, isBit: false, isWordGroup: false, out result, out errorMessage);
        }

        if (trimmed.StartsWith("RT", StringComparison.Ordinal) || trimmed.StartsWith("RC", StringComparison.Ordinal))
        {
            string twoCharPrefixes = trimmed.Substring(0, TwoCharPrefixLength);
            return TryParseCustom(trimmed, twoCharPrefixes, TwoCharPrefixLength, ShortAddressDigits, isBit: false, isWordGroup: false, out result, out errorMessage);
        }

        if (trimmed.Length >= TwoCharPrefixLength && trimmed[0] == 'W' && IsDiscretePrefix(trimmed[1]))
        {
            string devGroup = trimmed.Substring(0, TwoCharPrefixLength);
            return TryParseCustom(trimmed, devGroup, TwoCharPrefixLength, ShortAddressDigits, isBit: false, isWordGroup: true, out result, out errorMessage);
        }

        char prefix = trimmed[0];

        if (IsDiscretePrefix(prefix))
        {
            return TryParseCustom(trimmed, prefix.ToString(), OneCharPrefixLength, ShortAddressDigits, isBit: true, isWordGroup: false, out result, out errorMessage);
        }

        if (prefix is 'R' or 'D' or 'F')
        {
            return TryParseCustom(trimmed, prefix.ToString(), OneCharPrefixLength, WordAddressDigits, isBit: false, isWordGroup: false, out result, out errorMessage);
        }

        errorMessage = $"지원하지 않는 FATEK 디바이스 접두사입니다: {address}";
        return false;
    }

    private static bool TryParseCustom(
        string original,
        string devicePrefix,
        int prefixLen,
        int padDigits,
        bool isBit,
        bool isWordGroup,
        [NotNullWhen(true)] out FatekAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string numberText = original.Substring(prefixLen);
        int maxNum = padDigits == ShortAddressDigits ? MaxFourDigitIndex : MaxFiveDigitIndex;
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber) || parsedNumber < 0 || parsedNumber > maxNum)
        {
            errorMessage = $"유효하지 않은 FATEK 주소 번호입니다: {original}";
            return false;
        }

        string format = padDigits == ShortAddressDigits ? $"D{ShortAddressDigits}" : $"D{WordAddressDigits}";
        string wire = $"{devicePrefix}{parsedNumber.ToString(format, CultureInfo.InvariantCulture)}";

        result = new FatekAddress(original, devicePrefix, parsedNumber, isBit, isWordGroup, wire);
        return true;
    }

    private static bool IsDiscretePrefix(char c) => c is 'X' or 'Y' or 'M' or 'S' or 'T' or 'C';

    /// <summary>오프셋만큼 증가된 새로운 <see cref="FatekAddress"/>를 반환합니다.</summary>
    public FatekAddress Offset(int elementOffset)
    {
        if (elementOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elementOffset), elementOffset, "오프셋은 0 이상이어야 합니다.");
        }

        int stride = IsWordGroup ? WordGroupBitStride : 1;
        long nextIndex = Index + ((long)elementOffset * stride);
        string newOriginal = $"{Device}{nextIndex}";
        return Parse(newOriginal);
    }
}
