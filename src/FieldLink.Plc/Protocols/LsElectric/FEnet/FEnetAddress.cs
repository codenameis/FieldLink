using System;
using System.Globalization;

namespace FieldLink.Plc.Protocols.LsElectric.FEnet;

/// <summary>LS ELECTRIC XGT 전용 통신(FEnet) 주소 문자열을 파싱하고 관리하는 불변 값 객체입니다. (사양서 §2)</summary>
public sealed class FEnetAddress
{
    private FEnetAddress(string variableName, char area, char size, int offset)
    {
        VariableName = variableName;
        Area = area;
        Size = size;
        Offset = offset;
    }

    /// <summary>XGT 프로토콜 요청에 삽입되는 정규화된 변수명입니다. (예: "%DW100")</summary>
    public string VariableName { get; }

    /// <summary>메모리 영역 기호입니다. (예: 'D', 'M', 'P', 'K')</summary>
    public char Area { get; }

    /// <summary>변수 크기 단위입니다. ('X', 'B', 'W', 'D', 'L')</summary>
    public char Size { get; }

    /// <summary>시작 주소 오프셋입니다.</summary>
    public int Offset { get; }

    private const int BitUnitByteSize = 1;
    private const int ByteUnitSize = 1;
    private const int WordUnitByteSize = 2;
    private const int DWordUnitByteSize = 4;
    private const int LWordUnitByteSize = 8;
    private const int MinimumIecLength = 4;
    private const int IecOffsetIndex = 3;
    private const int MinimumLegacyLength = 2;
    private const int LegacyOffsetIndex = 1;
    private const char BitSizeCode = 'X';
    private const char ByteSizeCode = 'B';
    private const char WordSizeCode = 'W';
    private const char DWordSizeCode = 'D';
    private const char LWordSizeCode = 'L';
    private const string IecPrefix = "%";

    /// <summary>비트 단위 변수인지 여부입니다.</summary>
    public bool IsBit => Size == BitSizeCode;

    /// <summary>원소당 바이트 크기를 반환합니다.</summary>
    public int ElementByteSize => Size switch
    {
        BitSizeCode => BitUnitByteSize, // 비트 데이터 전송 시 바이트 단위
        ByteSizeCode => ByteUnitSize,
        WordSizeCode => WordUnitByteSize,
        DWordSizeCode => DWordUnitByteSize,
        LWordSizeCode => LWordUnitByteSize,
        _ => WordUnitByteSize
    };

    /// <summary>오프셋이 증가된 새 주소 인스턴스를 생성합니다.</summary>
    /// <param name="additionalElements">증가시킬 원소 수입니다.</param>
    public FEnetAddress WithOffset(int additionalElements)
    {
        if (additionalElements == 0)
        {
            return this;
        }

        int newOffset = checked(Offset + additionalElements);
        string newName = $"%{Area}{Size}{newOffset}";
        return new FEnetAddress(newName, Area, Size, newOffset);
    }

    internal FEnetAddress WithByteOffset(long additionalBytes)
    {
        if (additionalBytes == 0)
        {
            return this;
        }

        if (additionalBytes % ElementByteSize == 0)
        {
            return WithOffset(checked((int)(additionalBytes / ElementByteSize)));
        }

        int newByteOffset = checked((int)(((long)Offset * ElementByteSize) + additionalBytes));
        string newName = $"%{Area}{ByteSizeCode}{newByteOffset}";
        return new FEnetAddress(newName, Area, ByteSizeCode, newByteOffset);
    }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    /// <param name="address">IEC 또는 XGK 표기의 주소 문자열입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/>가 null일 때.</exception>
    /// <exception cref="FormatException">주소 형식이 올바르지 않을 때.</exception>
    public static FEnetAddress Parse(string address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!TryParse(address, out FEnetAddress? result) || result is null)
        {
            throw new FormatException($"유효하지 않은 XGT FEnet 주소 형식입니다: '{address}'");
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(string? address, out FEnetAddress? result)
    {
        result = null;
        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        string trimmed = address.Trim().ToUpperInvariant();
        if (trimmed.StartsWith(IecPrefix, StringComparison.Ordinal))
        {
            return TryParseIec(trimmed, out result);
        }

        return TryParseLegacy(trimmed, out result);
    }

    private static bool TryParseIec(string input, out FEnetAddress? result)
    {
        result = null;
        // 최소 길이 4: "%" + Area(1) + Size(1) + Offset(1자리 이상)
        if (input.Length < MinimumIecLength)
        {
            return false;
        }

        char area = input[1];
        if (!IsValidArea(area))
        {
            return false;
        }

        char size = input[2];
        if (!IsValidSize(size))
        {
            return false;
        }

        string offsetPart = input.Substring(IecOffsetIndex);
        if (!int.TryParse(offsetPart, NumberStyles.None, CultureInfo.InvariantCulture, out int offset) || offset < 0)
        {
            return false;
        }

        result = new FEnetAddress(input, area, size, offset);
        return true;
    }

    private static bool TryParseLegacy(string input, out FEnetAddress? result)
    {
        result = null;
        if (input.Length < MinimumLegacyLength)
        {
            return false;
        }

        char area = input[0];
        if (!IsValidArea(area))
        {
            return false;
        }

        string offsetPart = input.Substring(LegacyOffsetIndex);
        if (!int.TryParse(offsetPart, NumberStyles.None, CultureInfo.InvariantCulture, out int offset) || offset < 0)
        {
            return false;
        }

        // 레거시 XGK 기본은 워드(W) 단위 매핑
        char size = WordSizeCode;
        string normalizedName = $"%{area}{size}{offset}";
        result = new FEnetAddress(normalizedName, area, size, offset);
        return true;
    }

    private static bool IsValidArea(char area)
    {
        return area is 'P' or 'M' or 'K' or 'F' or 'T' or 'C' or 'U' or 'Z' or 'S' or 'D' or 'R' or 'N';
    }

    private static bool IsValidSize(char size)
    {
        return size is BitSizeCode or ByteSizeCode or WordSizeCode or DWordSizeCode or LWordSizeCode;
    }

    /// <inheritdoc />
    public override string ToString() => VariableName;
}
