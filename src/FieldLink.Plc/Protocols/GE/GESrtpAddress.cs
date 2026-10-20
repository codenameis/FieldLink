using System;
using System.Diagnostics.CodeAnalysis;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 프로토콜의 주소 표현입니다. (사양서 §1.4)</summary>
public sealed class GESrtpAddress
{
    private GESrtpAddress(string original, byte memoryTypeCode, ushort wireAddress, bool isBit, bool isWord)
    {
        Original = original;
        MemoryTypeCode = memoryTypeCode;
        WireAddress = wireAddress;
        IsBit = isBit;
        IsWord = isWord;
    }

    /// <summary>입력된 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>GFK-0582 메모리 세그먼트 셀렉터 코드입니다.</summary>
    public byte MemoryTypeCode { get; }

    /// <summary>와이어 전송 주소 (0-based)입니다.</summary>
    public ushort WireAddress { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>16비트 워드 전용 영역(R, AI, AQ) 여부입니다.</summary>
    public bool IsWord { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static GESrtpAddress Parse(string address, bool isBitAccess = false)
    {
        if (!TryParse(address, isBitAccess, out GESrtpAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        bool isBitAccess,
        [NotNullWhen(true)] out GESrtpAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (trimmed.StartsWith("%", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(1);
        }

        if (!TryExtractPrefix(trimmed, isBitAccess, out byte typeCode, out bool isBit, out bool isWord, out string numberText))
        {
            errorMessage = $"지원하지 않는 GE SRTP 주소 접두사입니다: {address}";
            return false;
        }

        if (!ushort.TryParse(numberText, out ushort userNum) || userNum == 0)
        {
            errorMessage = $"GE 주소 번호는 1 이상이어야 합니다: {address}";
            return false;
        }

        ushort wireAddr = (ushort)(userNum - 1);
        result = new GESrtpAddress(trimmed, typeCode, wireAddr, isBit, isWord);
        return true;
    }

    private static bool TryExtractPrefix(
        string trimmed,
        bool isBitAccess,
        out byte typeCode,
        out bool isBit,
        out bool isWord,
        out string numberText)
    {
        typeCode = 0;
        isBit = false;
        isWord = false;
        numberText = string.Empty;

        // 2글자 접두사 먼저 검사
        string[] prefixes2 = ["SA", "SB", "SC", "AI", "AQ"];
        foreach (string p2 in prefixes2)
        {
            if (trimmed.StartsWith(p2, StringComparison.Ordinal))
            {
                numberText = trimmed.Substring(2);
                return ResolveTypeCode(p2, isBitAccess, out typeCode, out isBit, out isWord);
            }
        }

        // 1글자 접두사
        string p1 = trimmed.Substring(0, 1);
        numberText = trimmed.Substring(1);
        return ResolveTypeCode(p1, isBitAccess, out typeCode, out isBit, out isWord);
    }

    private static bool ResolveTypeCode(
        string prefix,
        bool isBitAccess,
        out byte typeCode,
        out bool isBit,
        out bool isWord)
    {
        isBit = false;
        isWord = false;
        typeCode = prefix switch
        {
            "I" => (byte)(isBitAccess ? 0x46 : 0x10),
            "Q" => (byte)(isBitAccess ? 0x48 : 0x12),
            "T" => (byte)(isBitAccess ? 0x4A : 0x14),
            "M" => (byte)(isBitAccess ? 0x4C : 0x16),
            "SA" => (byte)(isBitAccess ? 0x4E : 0x18),
            "SB" => (byte)(isBitAccess ? 0x50 : 0x1A),
            "SC" => (byte)(isBitAccess ? 0x52 : 0x1C),
            "S" => (byte)(isBitAccess ? 0x54 : 0x1E),
            "G" => (byte)(isBitAccess ? 0x56 : 0x38),
            "R" => 0x08,
            "AI" => 0x0A,
            "AQ" => 0x0C,
            _ => 0,
        };

        if (typeCode == 0)
        {
            return false;
        }

        isBit = isBitAccess && typeCode >= 0x46;
        isWord = typeCode is 0x08 or 0x0A or 0x0C;
        return true;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="GESrtpAddress"/>를 생성합니다.</summary>
    public GESrtpAddress Offset(int count)
    {
        ushort newWire = (ushort)(WireAddress + count);
        return new GESrtpAddress($"{Original}+{count}", MemoryTypeCode, newWire, IsBit, IsWord);
    }
}
