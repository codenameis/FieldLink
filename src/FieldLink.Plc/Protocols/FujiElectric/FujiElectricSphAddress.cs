using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 프로토콜의 주소 표현입니다. (사양서 Part C §C.1.2)</summary>
public sealed class FujiElectricSphAddress
{
    private const byte IoAreaCode = 0x01;
    private const byte StandardMemoryAreaCode = 0x02;
    private const byte RetainMemoryAreaCode = 0x04;
    private const byte SystemMemoryAreaCode = 0x08;
    private const uint MaxWordAddress = 0x00FFFFFFu;
    private const byte MaxBitOffset = 15;
    private const int StandardPrefixLength = 3;
    private const int SystemPrefixLength = 4;
    private const int IoPrefixLength = 1;

    private FujiElectricSphAddress(string original, byte areaCode, uint wordAddress, byte bitOffset, bool isBit)
    {
        Original = original;
        AreaCode = areaCode;
        WordAddress = wordAddress;
        BitOffset = bitOffset;
        IsBit = isBit;
    }

    /// <summary>입력된 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>SX-Bus 메모리 영역 코드입니다. (0x01: I/Q, 0x02: M1, 0x04: M3, 0x08: M10)</summary>
    public byte AreaCode { get; }

    /// <summary>24비트 워드 오프셋(0~16777215)입니다.</summary>
    public uint WordAddress { get; }

    /// <summary>워드 내 비트 오프셋(0~15)입니다.</summary>
    public byte BitOffset { get; }

    /// <summary>비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    /// <param name="address">파싱할 SPH 주소 문자열입니다.</param>
    /// <returns>파싱된 <see cref="FujiElectricSphAddress"/> 인스턴스입니다.</returns>
    public static FujiElectricSphAddress Parse(string address)
    {
        if (!TryParse(address, out FujiElectricSphAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    /// <param name="address">파싱할 SPH 주소 문자열입니다.</param>
    /// <param name="result">파싱에 성공한 주소 객체입니다.</param>
    /// <param name="errorMessage">파싱 실패 시 오류 설명입니다.</param>
    /// <returns>파싱 성공 여부입니다.</returns>
    public static bool TryParse(
        string? address,
        [NotNullWhen(true)] out FujiElectricSphAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (!TryExtractAreaCode(trimmed, out byte areaCode, out string numberText))
        {
            errorMessage = $"지원하지 않는 SPH 메모리 영역입니다: {address}";
            return false;
        }

        return TryParseWordAndBit(trimmed, areaCode, numberText, out result, out errorMessage);
    }

    /// <summary>워드 오프셋을 증가시킨 새 주소를 반환합니다.</summary>
    /// <param name="wordCount">이동할 워드 개수입니다.</param>
    /// <returns>오프셋이 반영된 새 <see cref="FujiElectricSphAddress"/> 인스턴스입니다.</returns>
    public FujiElectricSphAddress Offset(int wordCount)
    {
        uint newWordAddress = (uint)(WordAddress + wordCount);
        return new FujiElectricSphAddress(Original, AreaCode, newWordAddress, BitOffset, IsBit);
    }

    internal FujiElectricSphAddress ToWordAddress(uint wordAddress)
    {
        return new FujiElectricSphAddress(Original, AreaCode, wordAddress, bitOffset: 0, isBit: false);
    }

    private static bool TryExtractAreaCode(string trimmed, out byte areaCode, out string numberText)
    {
        if (trimmed.StartsWith("M1.", StringComparison.Ordinal))
        {
            areaCode = StandardMemoryAreaCode;
            numberText = trimmed.Substring(StandardPrefixLength);
            return true;
        }

        if (trimmed.StartsWith("M3.", StringComparison.Ordinal))
        {
            areaCode = RetainMemoryAreaCode;
            numberText = trimmed.Substring(StandardPrefixLength);
            return true;
        }

        if (trimmed.StartsWith("M10.", StringComparison.Ordinal))
        {
            areaCode = SystemMemoryAreaCode;
            numberText = trimmed.Substring(SystemPrefixLength);
            return true;
        }

        if (trimmed.StartsWith("I", StringComparison.Ordinal) || trimmed.StartsWith("Q", StringComparison.Ordinal))
        {
            areaCode = IoAreaCode;
            numberText = trimmed.Substring(IoPrefixLength);
            return true;
        }

        areaCode = 0;
        numberText = string.Empty;
        return false;
    }

    private static bool TryParseWordAndBit(
        string original,
        byte areaCode,
        string numberText,
        [NotNullWhen(true)] out FujiElectricSphAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        int dotIndex = numberText.IndexOf('.');
        if (dotIndex >= 0)
        {
            return TryParseBitAddress(original, areaCode, numberText, dotIndex, out result, out errorMessage);
        }

        if (!uint.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint wordAddress)
            || wordAddress > MaxWordAddress)
        {
            errorMessage = $"유효하지 않은 워드 주소입니다: {original}";
            return false;
        }

        result = new FujiElectricSphAddress(original, areaCode, wordAddress, bitOffset: 0, isBit: false);
        return true;
    }

    private static bool TryParseBitAddress(
        string original,
        byte areaCode,
        string numberText,
        int dotIndex,
        [NotNullWhen(true)] out FujiElectricSphAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string wordPart = numberText.Substring(0, dotIndex);
        string bitPart = numberText.Substring(dotIndex + 1);

        if (!uint.TryParse(wordPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedWordAddress)
            || parsedWordAddress > MaxWordAddress)
        {
            errorMessage = $"유효하지 않은 워드 주소입니다: {original}";
            return false;
        }

        if (!byte.TryParse(bitPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte parsedBitOffset)
            || parsedBitOffset > MaxBitOffset)
        {
            errorMessage = $"유효하지 않은 비트 오프셋입니다: {original}";
            return false;
        }

        result = new FujiElectricSphAddress(original, areaCode, parsedWordAddress, parsedBitOffset, isBit: true);
        return true;
    }
}
