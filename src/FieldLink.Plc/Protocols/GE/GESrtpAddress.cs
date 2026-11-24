using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.GE;

/// <summary>GE Fanuc / Emerson SRTP 프로토콜의 주소 표현입니다. (사양서 §1.4)</summary>
public sealed class GESrtpAddress
{
    private const byte DiscreteInputBitCode = 0x46;
    private const byte DiscreteInputByteCode = 0x10;
    private const byte DiscreteOutputBitCode = 0x48;
    private const byte DiscreteOutputByteCode = 0x12;
    private const byte TemporaryMemoryBitCode = 0x4A;
    private const byte TemporaryMemoryByteCode = 0x14;
    private const byte InternalMarkerBitCode = 0x4C;
    private const byte InternalMarkerByteCode = 0x16;
    private const byte SystemStatusABitCode = 0x4E;
    private const byte SystemStatusAByteCode = 0x18;
    private const byte SystemStatusBBitCode = 0x50;
    private const byte SystemStatusBByteCode = 0x1A;
    private const byte SystemStatusCBitCode = 0x52;
    private const byte SystemStatusCByteCode = 0x1C;
    private const byte SystemStatusBitCode = 0x54;
    private const byte SystemStatusByteCode = 0x1E;
    private const byte GeniusGlobalBitCode = 0x56;
    private const byte GeniusGlobalByteCode = 0x38;
    private const byte RegisterMemoryWordCode = 0x08;
    private const byte AnalogInputWordCode = 0x0A;
    private const byte AnalogOutputWordCode = 0x0C;
    private const int TwoCharPrefixLength = 2;
    private const int SingleCharPrefixLength = 1;

    private static readonly string[] TwoCharacterPrefixes = ["SA", "SB", "SC", "AI", "AQ"];

    private GESrtpAddress(string original, byte memoryTypeCode, ushort wireAddress, bool isBit, bool isWord)
    {
        Original = original;
        MemoryTypeCode = memoryTypeCode;
        WireAddress = wireAddress;
        IsBit = isBit;
        IsWord = isWord;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>GFK-0582 메모리 세그먼트 셀렉터 코드입니다.</summary>
    public byte MemoryTypeCode { get; }

    /// <summary>와이어 전송 주소(0-based)입니다.</summary>
    public ushort WireAddress { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>16비트 워드 전용 영역(R, AI, AQ) 여부입니다.</summary>
    public bool IsWord { get; }

    /// <summary>주소 문자열을 해석하여 <see cref="GESrtpAddress"/>를 생성합니다.</summary>
    /// <param name="address">제조사 표기 주소 문자열입니다 (예: "M1", "%R100", "AI10").</param>
    /// <param name="isBitAccess">이산 영역을 비트 단위(0x46~0x56)로 접근할지 여부입니다.</param>
    /// <returns>해석된 주소 객체입니다.</returns>
    /// <exception cref="ArgumentException"><paramref name="address"/>가 비어 있을 때.</exception>
    /// <exception cref="FormatException">주소 형식이 올바르지 않을 때.</exception>
    public static GESrtpAddress Parse(string address, bool isBitAccess = false)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!TryParse(address, isBitAccess, out GESrtpAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 해석을 시도합니다.</summary>
    /// <param name="address">제조사 표기 주소 문자열입니다.</param>
    /// <param name="isBitAccess">이산 영역을 비트 단위(0x46~0x56)로 접근할지 여부입니다.</param>
    /// <param name="result">해석에 성공했을 때 생성된 주소 객체입니다.</param>
    /// <param name="errorMessage">해석에 실패했을 때의 오류 설명입니다.</param>
    /// <returns>해석 성공 여부입니다.</returns>
    public static bool TryParse(
        string? address,
        bool isBitAccess,
        [NotNullWhen(true)] out GESrtpAddress? result,
        [NotNullWhen(false)] out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            errorMessage = "주소 문자열이 비어 있습니다.";
            return false;
        }

        string trimmedAddress = address.Trim().ToUpperInvariant();
        if (trimmedAddress.StartsWith("%", StringComparison.Ordinal))
        {
            trimmedAddress = trimmedAddress.Substring(SingleCharPrefixLength);
        }

        if (trimmedAddress.Length == 0 || !TryExtractPrefix(trimmedAddress, isBitAccess, out byte typeCode, out bool isBit, out bool isWord, out string numberText))
        {
            errorMessage = $"지원하지 않는 GE SRTP 주소 접두사입니다: {address}";
            return false;
        }

        if (!ushort.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out ushort userNumber) || userNumber == 0)
        {
            errorMessage = $"GE 주소 번호는 1 이상이어야 합니다: {address}";
            return false;
        }

        ushort wireAddress = (ushort)(userNumber - 1);
        result = new GESrtpAddress(trimmedAddress, typeCode, wireAddress, isBit, isWord);
        return true;
    }

    /// <summary>지정한 단위 오프셋만큼 주소를 이동한 새 <see cref="GESrtpAddress"/>를 생성합니다.</summary>
    /// <param name="unitOffset">이동할 단위 오프셋(워드, 바이트 또는 비트 수)입니다.</param>
    /// <returns>오프셋이 반영된 새 주소 객체입니다.</returns>
    public GESrtpAddress Offset(int unitOffset)
    {
        if (unitOffset < 0 || WireAddress + (long)unitOffset > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(unitOffset), unitOffset, "SRTP 주소 오프셋이 16비트 범위를 초과합니다.");
        }

        ushort nextWireAddress = (ushort)(WireAddress + unitOffset);
        return new GESrtpAddress($"{Original}+{unitOffset}", MemoryTypeCode, nextWireAddress, IsBit, IsWord);
    }

    private static bool TryExtractPrefix(
        string trimmedAddress,
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

        // GFK-0582 §1.4: 2글자 접두사(SA, SB, SC, AI, AQ)를 1글자보다 먼저 검사한다.
        foreach (string twoCharPrefix in TwoCharacterPrefixes)
        {
            if (trimmedAddress.StartsWith(twoCharPrefix, StringComparison.Ordinal))
            {
                numberText = trimmedAddress.Substring(TwoCharPrefixLength);
                return ResolveTypeCode(twoCharPrefix, isBitAccess, out typeCode, out isBit, out isWord);
            }
        }

        string singleCharPrefix = trimmedAddress.Substring(0, SingleCharPrefixLength);
        numberText = trimmedAddress.Substring(SingleCharPrefixLength);
        return ResolveTypeCode(singleCharPrefix, isBitAccess, out typeCode, out isBit, out isWord);
    }

    private static bool ResolveTypeCode(
        string prefix,
        bool isBitAccess,
        out byte typeCode,
        out bool isBit,
        out bool isWord)
    {
        typeCode = SelectMemoryTypeCode(prefix, isBitAccess);
        if (typeCode == 0)
        {
            isBit = false;
            isWord = false;
            return false;
        }

        isBit = isBitAccess && typeCode >= DiscreteInputBitCode;
        isWord = typeCode is RegisterMemoryWordCode or AnalogInputWordCode or AnalogOutputWordCode;
        return true;
    }

    private static byte SelectMemoryTypeCode(string prefix, bool isBitAccess) => prefix switch
    {
        "I" => isBitAccess ? DiscreteInputBitCode : DiscreteInputByteCode,
        "Q" => isBitAccess ? DiscreteOutputBitCode : DiscreteOutputByteCode,
        "T" => isBitAccess ? TemporaryMemoryBitCode : TemporaryMemoryByteCode,
        "M" => isBitAccess ? InternalMarkerBitCode : InternalMarkerByteCode,
        "SA" => isBitAccess ? SystemStatusABitCode : SystemStatusAByteCode,
        "SB" => isBitAccess ? SystemStatusBBitCode : SystemStatusBByteCode,
        "SC" => isBitAccess ? SystemStatusCBitCode : SystemStatusCByteCode,
        "S" => isBitAccess ? SystemStatusBitCode : SystemStatusByteCode,
        "G" => isBitAccess ? GeniusGlobalBitCode : GeniusGlobalByteCode,
        "R" => RegisterMemoryWordCode,
        "AI" => AnalogInputWordCode,
        "AQ" => AnalogOutputWordCode,
        _ => 0,
    };
}
