using System;
using System.Diagnostics.CodeAnalysis;

namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary>Fuji SPB 프로토콜의 주소 표현입니다. (사양서 Part A)</summary>
public sealed class FujiElectricSpbAddress
{
    private FujiElectricSpbAddress(string original, string areaCode, ushort wordAddress, byte bitOffset, bool isBit, string wireWordAddress)
    {
        Original = original;
        AreaCode = areaCode;
        WordAddress = wordAddress;
        BitOffset = bitOffset;
        IsBit = isBit;
        WireWordAddress = wireWordAddress;
    }

    /// <summary>입력된 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>2자리 16진 텍스트 영역 코드입니다. (예: "0C" for D, "02" for M)</summary>
    public string AreaCode { get; }

    /// <summary>16비트 워드 번호입니다.</summary>
    public ushort WordAddress { get; }

    /// <summary>워드 내 비트 오프셋 (0~15)입니다.</summary>
    public byte BitOffset { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>워드 접근 시 6문자 주소 문자열입니다. (영역코드2 + 자리교차4문자)</summary>
    public string WireWordAddress { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static FujiElectricSpbAddress Parse(string address)
    {
        if (!TryParse(address, out FujiElectricSpbAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        [NotNullWhen(true)] out FujiElectricSpbAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (!TryExtractPrefix(trimmed, out _, out string areaCode, out bool isBitArea, out string numberText))
        {
            errorMessage = $"지원하지 않는 Fuji SPB 주소 접두사입니다: {address}";
            return false;
        }

        return TryParseNumber(trimmed, areaCode, isBitArea, numberText, out result, out errorMessage);
    }

    private static bool TryExtractPrefix(
        string trimmed,
        out string prefix,
        out string areaCode,
        out bool isBitArea,
        out string numberText)
    {
        prefix = string.Empty;
        areaCode = string.Empty;
        isBitArea = false;
        numberText = string.Empty;

        string[] prefixes2 = ["TC", "CC", "TN", "CN"];
        foreach (string p2 in prefixes2)
        {
            if (trimmed.StartsWith(p2, StringComparison.Ordinal))
            {
                prefix = p2;
                numberText = trimmed.Substring(2);
                areaCode = p2 switch
                {
                    "TC" => "04",
                    "CC" => "05",
                    "TN" => "0A",
                    "CN" => "0B",
                    _ => string.Empty,
                };
                isBitArea = p2 is "TC" or "CC";
                return true;
            }
        }

        char p1 = trimmed[0];
        numberText = trimmed.Substring(1);
        prefix = p1.ToString();
        isBitArea = p1 is 'Y' or 'X' or 'M' or 'L';

        areaCode = p1 switch
        {
            'Y' => "00",
            'X' => "01",
            'M' => "02",
            'L' => "03",
            'D' => "0C",
            'R' => "0D",
            'W' => "0E",
            _ => string.Empty,
        };

        return !string.IsNullOrEmpty(areaCode);
    }

    private static bool TryParseNumber(
        string original,
        string areaCode,
        bool isBitArea,
        string numberText,
        [NotNullWhen(true)] out FujiElectricSpbAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        int dotIndex = numberText.IndexOf('.');
        if (dotIndex >= 0)
        {
            return TryParseDotAddress(original, areaCode, numberText, dotIndex, out result, out errorMessage);
        }

        if (!ushort.TryParse(numberText, out ushort parsedNumber))
        {
            errorMessage = $"Fuji SPB 주소 번호가 올바르지 않습니다: {original}";
            return false;
        }

        ushort wordAddr;
        byte bitOffset;
        if (isBitArea)
        {
            wordAddr = (ushort)(parsedNumber / 16);
            bitOffset = (byte)(parsedNumber % 16);
        }
        else
        {
            wordAddr = parsedNumber;
            bitOffset = 0;
        }

        string swapped = FormatSwapped4(wordAddr);
        string wire = $"{areaCode}{swapped}";
        result = new FujiElectricSpbAddress(original, areaCode, wordAddr, bitOffset, isBitArea, wire);
        return true;
    }

    private static bool TryParseDotAddress(
        string original,
        string areaCode,
        string numberText,
        int dotIndex,
        [NotNullWhen(true)] out FujiElectricSpbAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string wordStr = numberText.Substring(0, dotIndex);
        string bitStr = numberText.Substring(dotIndex + 1);

        if (!ushort.TryParse(wordStr, out ushort wordAddr))
        {
            errorMessage = $"워드 주소 파싱 실패: {original}";
            return false;
        }

        if (!byte.TryParse(bitStr, System.Globalization.NumberStyles.HexNumber, null, out byte bitOffset) || bitOffset > 15)
        {
            errorMessage = $"비트 오프셋 파싱 실패 (0~F): {original}";
            return false;
        }

        string swapped = FormatSwapped4(wordAddr);
        string wire = $"{areaCode}{swapped}";
        result = new FujiElectricSpbAddress(original, areaCode, wordAddr, bitOffset, isBit: true, wire);
        return true;
    }

    /// <summary>10진 ushort 값을 4자리로 만들고 상위 2자리와 하위 2자리를 맞바꿉니다. (예: 100 -> "0100" -> "0001")</summary>
    public static string FormatSwapped4(ushort value)
    {
        string d4 = value.ToString("D4");
        return d4.Substring(2, 2) + d4.Substring(0, 2);
    }

    /// <summary>상·하위 교차된 4자리 10진 문자열을 ushort로 복원합니다. (예: "0001" -> "0100" -> 100)</summary>
    public static ushort Unswap4(string swapped4)
    {
        if (swapped4.Length != 4)
        {
            throw new ArgumentException("4자리 문자열이어야 합니다.", nameof(swapped4));
        }

        string d4 = swapped4.Substring(2, 2) + swapped4.Substring(0, 2);
        return ushort.Parse(d4);
    }

    /// <summary>단일 비트 쓰기용 8문자 주소를 생성합니다. (영역코드2 + 자리교차4문자(바이트인덱스) + 바이트내비트2자리)</summary>
    public string BuildBitWriteAddress()
    {
        int byteIndex = (WordAddress * 2) + (BitOffset / 8);
        int bitInByte = BitOffset % 8;
        string swappedByte = FormatSwapped4((ushort)byteIndex);
        return $"{AreaCode}{swappedByte}{bitInByte:D2}";
    }

    /// <summary>오프셋만큼 워드 또는 비트 주소를 이동한 새 인스턴스를 반환합니다.</summary>
    public FujiElectricSpbAddress Offset(int count)
    {
        if (IsBit)
        {
            int totalBits = (WordAddress * 16) + BitOffset + count;
            ushort newWord = (ushort)(totalBits / 16);
            byte newBit = (byte)(totalBits % 16);
            string wire = $"{AreaCode}{FormatSwapped4(newWord)}";
            return new FujiElectricSpbAddress($"{AreaCode}.{totalBits}", AreaCode, newWord, newBit, true, wire);
        }
        else
        {
            ushort newWord = (ushort)(WordAddress + count);
            string wire = $"{AreaCode}{FormatSwapped4(newWord)}";
            return new FujiElectricSpbAddress($"{AreaCode}{newWord}", AreaCode, newWord, 0, false, wire);
        }
    }
}
