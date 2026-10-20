using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary>S7 절대 주소 문자열(예: "DB1.DBW10", "M10.0", "IB0")을 해석한 불변 값 객체입니다. (사양서 §2)</summary>
public sealed class S7CommAddress
{
    private const int MaxDataBlockNumber = 65535;
    private const int MaxByteOffset = 2097151;
    private const int BitsPerByte = 8;

    private static readonly Regex DataBlockPattern = new(
        @"^DB(?<block>\d+)\.DB(?<size>[XBWD])(?<byte>\d+)(\.(?<bit>[0-7]))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DirectAreaPattern = new(
        @"^(?<area>[IQM])(?<size>[XBWD])?(?<byte>\d+)(\.(?<bit>[0-7]))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private S7CommAddress(
        string originalAddress,
        S7CommArea area,
        int dataBlockNumber,
        int byteOffset,
        int bitNumber,
        S7CommAccessSize accessSize)
    {
        OriginalAddress = originalAddress;
        Area = area;
        DataBlockNumber = dataBlockNumber;
        ByteOffset = byteOffset;
        BitNumber = bitNumber;
        AccessSize = accessSize;
    }

    /// <summary>사용자가 입력한 원본 주소입니다.</summary>
    public string OriginalAddress { get; }

    /// <summary>메모리 영역입니다.</summary>
    public S7CommArea Area { get; }

    /// <summary>데이터 블록 번호입니다. DB 영역이 아니면 0입니다.</summary>
    public int DataBlockNumber { get; }

    /// <summary>시작 바이트 오프셋입니다.</summary>
    public int ByteOffset { get; }

    /// <summary>비트 번호(0~7)입니다. 비트 접근이 아니면 0입니다.</summary>
    public int BitNumber { get; }

    /// <summary>주소 표기의 크기 문자가 뜻하는 접근 단위입니다.</summary>
    public S7CommAccessSize AccessSize { get; }

    /// <summary>비트 접근 주소인지 여부입니다.</summary>
    public bool IsBit => AccessSize == S7CommAccessSize.Bit;

    /// <summary>요청 항목의 시작 주소(비트 단위)입니다. 바이트 접근은 바이트 오프셋 × 8, 비트 접근은 거기에 비트 번호를 더합니다.</summary>
    public int StartBitAddress => (ByteOffset * BitsPerByte) + BitNumber;

    /// <summary>주소 문자열을 해석합니다.</summary>
    /// <param name="address">해석할 주소 문자열입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/>가 null일 때.</exception>
    /// <exception cref="FormatException">주소 형식이 올바르지 않을 때.</exception>
    public static S7CommAddress Parse(string address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!TryParse(address, out S7CommAddress? result))
        {
            throw new FormatException($"유효하지 않은 S7 주소입니다: '{address}'");
        }

        return result;
    }

    /// <summary>주소 문자열 해석을 시도합니다.</summary>
    public static bool TryParse(string? address, [NotNullWhen(true)] out S7CommAddress? result)
    {
        result = null;
        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        string trimmed = address.Trim();
        Match dataBlockMatch = DataBlockPattern.Match(trimmed);
        if (dataBlockMatch.Success)
        {
            return TryCreate(trimmed, S7CommArea.DataBlock, dataBlockMatch, out result);
        }

        Match directMatch = DirectAreaPattern.Match(trimmed);
        if (directMatch.Success)
        {
            S7CommArea area = GetDirectArea(directMatch.Groups["area"].Value[0]);
            return TryCreate(trimmed, area, directMatch, out result);
        }

        return false;
    }

    /// <summary>오프셋을 옮긴 새 주소를 만듭니다. 분할 전송에서 사용합니다.</summary>
    /// <param name="elementCount">옮길 개수입니다. 비트 주소는 비트 수, 그 밖에는 바이트 수입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">음수이거나 결과가 주소 범위를 넘을 때.</exception>
    public S7CommAddress WithOffset(int elementCount)
    {
        if (elementCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "오프셋은 0 이상이어야 합니다.");
        }

        long shiftedBitAddress = IsBit ? StartBitAddress + (long)elementCount : 0;
        long shiftedByteOffset = IsBit ? shiftedBitAddress / BitsPerByte : ByteOffset + (long)elementCount;

        if (shiftedByteOffset > MaxByteOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "바이트 오프셋이 주소 범위를 넘습니다.");
        }

        int shiftedBitNumber = IsBit ? (int)(shiftedBitAddress % BitsPerByte) : 0;
        return new S7CommAddress(OriginalAddress, Area, DataBlockNumber, (int)shiftedByteOffset, shiftedBitNumber, AccessSize);
    }

    /// <inheritdoc />
    public override string ToString() => OriginalAddress;

    private static bool TryCreate(string original, S7CommArea area, Match match, [NotNullWhen(true)] out S7CommAddress? result)
    {
        result = null;
        int dataBlockNumber = 0;
        if (area == S7CommArea.DataBlock && !TryReadNumber(match.Groups["block"].Value, 1, MaxDataBlockNumber, out dataBlockNumber))
        {
            return false;
        }

        if (!TryReadNumber(match.Groups["byte"].Value, 0, MaxByteOffset, out int byteOffset))
        {
            return false;
        }

        S7CommAccessSize accessSize = GetAccessSize(match.Groups["size"]);
        bool hasBitNumber = match.Groups["bit"].Success;
        if ((accessSize == S7CommAccessSize.Bit) != hasBitNumber)
        {
            return false;
        }

        int bitNumber = hasBitNumber ? int.Parse(match.Groups["bit"].Value, CultureInfo.InvariantCulture) : 0;
        result = new S7CommAddress(original, area, dataBlockNumber, byteOffset, bitNumber, accessSize);
        return true;
    }

    private static bool TryReadNumber(string text, int minimum, int maximum, out int number)
    {
        bool isParsed = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        return isParsed && number >= minimum && number <= maximum;
    }

    private static S7CommArea GetDirectArea(char areaLetter)
    {
        return char.ToUpperInvariant(areaLetter) switch
        {
            'I' => S7CommArea.ProcessInput,
            'Q' => S7CommArea.ProcessOutput,
            _ => S7CommArea.Memory
        };
    }

    private static S7CommAccessSize GetAccessSize(Group sizeGroup)
    {
        if (!sizeGroup.Success)
        {
            return S7CommAccessSize.Bit;
        }

        return char.ToUpperInvariant(sizeGroup.Value[0]) switch
        {
            'B' => S7CommAccessSize.Byte,
            'W' => S7CommAccessSize.Word,
            'D' => S7CommAccessSize.DoubleWord,
            _ => S7CommAccessSize.Bit
        };
    }
}
