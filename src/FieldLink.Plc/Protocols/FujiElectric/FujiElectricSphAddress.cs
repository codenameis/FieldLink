using System;
using System.Diagnostics.CodeAnalysis;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>MICREX-SX SPH Ethernet 프로토콜의 주소 표현입니다. (사양서 Part C §C.1.2)</summary>
public sealed class FujiElectricSphAddress
{
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

    /// <summary>24비트 워드 오프셋 (0~16777215)입니다.</summary>
    public uint WordAddress { get; }

    /// <summary>워드 내 비트 오프셋 (0~15)입니다.</summary>
    public byte BitOffset { get; }

    /// <summary>비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static FujiElectricSphAddress Parse(string address)
    {
        if (!TryParse(address, out FujiElectricSphAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
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
        byte areaCode;
        string numberText;

        if (trimmed.StartsWith("M1.", StringComparison.Ordinal))
        {
            areaCode = 0x02;
            numberText = trimmed.Substring(3);
        }
        else if (trimmed.StartsWith("M3.", StringComparison.Ordinal))
        {
            areaCode = 0x04;
            numberText = trimmed.Substring(3);
        }
        else if (trimmed.StartsWith("M10.", StringComparison.Ordinal))
        {
            areaCode = 0x08;
            numberText = trimmed.Substring(4);
        }
        else if (trimmed.StartsWith("I", StringComparison.Ordinal) || trimmed.StartsWith("Q", StringComparison.Ordinal))
        {
            areaCode = 0x01;
            numberText = trimmed.Substring(1);
        }
        else
        {
            errorMessage = $"지원하지 않는 SPH 메모리 영역입니다: {address}";
            return false;
        }

        int dotIdx = numberText.IndexOf('.');
        if (dotIdx >= 0)
        {
            string wordStr = numberText.Substring(0, dotIdx);
            string bitStr = numberText.Substring(dotIdx + 1);

            if (!uint.TryParse(wordStr, out uint w) || w > 0x00FFFFFF)
            {
                errorMessage = $"유효하지 않은 워드 주소입니다: {address}";
                return false;
            }

            if (!byte.TryParse(bitStr, System.Globalization.NumberStyles.HexNumber, null, out byte b) || b > 15)
            {
                errorMessage = $"유효하지 않은 비트 오프셋입니다: {address}";
                return false;
            }

            result = new FujiElectricSphAddress(trimmed, areaCode, w, b, isBit: true);
            return true;
        }

        if (!uint.TryParse(numberText, out uint wordNum) || wordNum > 0x00FFFFFF)
        {
            errorMessage = $"유효하지 않은 워드 주소입니다: {address}";
            return false;
        }

        result = new FujiElectricSphAddress(trimmed, areaCode, wordNum, 0, isBit: false);
        return true;
    }

    /// <summary>워드 오프셋을 증가시킨 새 주소를 반환합니다.</summary>
    public FujiElectricSphAddress Offset(int wordCount)
    {
        uint newWord = (uint)(WordAddress + wordCount);
        return new FujiElectricSphAddress($"{AreaCode}.{newWord}", AreaCode, newWord, BitOffset, IsBit);
    }
}
