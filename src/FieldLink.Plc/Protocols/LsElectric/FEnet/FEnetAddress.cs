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

    /// <summary>비트 단위 변수인지 여부입니다.</summary>
    public bool IsBit => Size == 'X';

    /// <summary>원소당 바이트 크기를 반환합니다.</summary>
    public int ElementByteSize => Size switch
    {
        'X' => 1, // 비트 데이터 전송 시 바이트 단위
        'B' => 1,
        'W' => 2,
        'D' => 4,
        'L' => 8,
        _ => 2
    };

    /// <summary>오프셋이 증가된 새 주소 인스턴스를 생성합니다.</summary>
    /// <param name="additionalElements">증가시킬 원소 수입니다.</param>
    public FEnetAddress WithOffset(int additionalElements)
    {
        if (additionalElements == 0)
        {
            return this;
        }

        int newOffset = Offset + additionalElements;
        string newName = $"%{Area}{Size}{newOffset}";
        return new FEnetAddress(newName, Area, Size, newOffset);
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
        if (trimmed.StartsWith("%", StringComparison.Ordinal))
        {
            return TryParseIec(trimmed, out result);
        }

        return TryParseLegacy(trimmed, out result);
    }

    private static bool TryParseIec(string input, out FEnetAddress? result)
    {
        result = null;
        // 최소 길이 4: "%" + Area(1) + Size(1) + Offset(1자리 이상)
        if (input.Length < 4)
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

        string offsetPart = input.Substring(3);
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
        if (input.Length < 2)
        {
            return false;
        }

        char area = input[0];
        if (!IsValidArea(area))
        {
            return false;
        }

        string offsetPart = input.Substring(1);
        if (!int.TryParse(offsetPart, NumberStyles.None, CultureInfo.InvariantCulture, out int offset) || offset < 0)
        {
            return false;
        }

        // 레거시 XGK 기본은 워드(W) 단위 매핑
        char size = 'W';
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
        return size is 'X' or 'B' or 'W' or 'D' or 'L';
    }

    /// <inheritdoc />
    public override string ToString() => VariableName;
}
