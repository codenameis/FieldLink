using System;
using System.Diagnostics.CodeAnalysis;

namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 컴포넌트 주소 값 객체입니다. (사양서 §1.2)</summary>
public sealed class FatekAddress
{
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
            return TryParseCustom(trimmed, "DRC", 3, 4, isBit: false, isWordGroup: false, out result, out errorMessage);
        }

        if (trimmed.StartsWith("RT", StringComparison.Ordinal) || trimmed.StartsWith("RC", StringComparison.Ordinal))
        {
            string twoCharPrefixes = trimmed.Substring(0, 2);
            return TryParseCustom(trimmed, twoCharPrefixes, 2, 4, isBit: false, isWordGroup: false, out result, out errorMessage);
        }

        if (trimmed.Length >= 2 && trimmed[0] == 'W' && IsDiscretePrefix(trimmed[1]))
        {
            string devGroup = trimmed.Substring(0, 2);
            return TryParseCustom(trimmed, devGroup, 2, 4, isBit: false, isWordGroup: true, out result, out errorMessage);
        }

        char prefix = trimmed[0];

        if (IsDiscretePrefix(prefix))
        {
            return TryParseCustom(trimmed, prefix.ToString(), 1, 4, isBit: true, isWordGroup: false, out result, out errorMessage);
        }

        if (prefix is 'R' or 'D' or 'F')
        {
            return TryParseCustom(trimmed, prefix.ToString(), 1, 5, isBit: false, isWordGroup: false, out result, out errorMessage);
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
        int maxNum = padDigits == 4 ? 9999 : 65535;
        if (!int.TryParse(numberText, out int parsedNumber) || parsedNumber < 0 || parsedNumber > maxNum)
        {
            errorMessage = $"유효하지 않은 FATEK 주소 번호입니다: {original}";
            return false;
        }

        string format = padDigits == 4 ? "D4" : "D5";
        string wire = $"{devicePrefix}{parsedNumber.ToString(format)}";

        result = new FatekAddress(original, devicePrefix, parsedNumber, isBit, isWordGroup, wire);
        return true;
    }

    private static bool IsDiscretePrefix(char c) => c is 'X' or 'Y' or 'M' or 'S' or 'T' or 'C';

    /// <summary>오프셋만큼 증가된 새로운 <see cref="FatekAddress"/>를 반환합니다.</summary>
    public FatekAddress Offset(int elementOffset)
    {
        int nextIndex = IsWordGroup ? Index + (elementOffset * 16) : Index + elementOffset;
        string newOriginal = $"{Device}{nextIndex}";
        return Parse(newOriginal);
    }
}
