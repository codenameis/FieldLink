using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 higher-level link service 디바이스 주소 표현입니다. (사양서 §1.3)</summary>
public sealed class YokogawaAddress
{
    private YokogawaAddress(
        string original,
        string device,
        int number,
        ushort attribute,
        bool isBit)
    {
        Original = original;
        Device = device;
        Number = number;
        Attribute = attribute;
        IsBit = isBit;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>디바이스 접두사입니다. (예: "D", "X", "Y", "TS", "CP")</summary>
    public string Device { get; }

    /// <summary>4바이트 절대 번호입니다.</summary>
    public int Number { get; }

    /// <summary>Yokogawa 2바이트 디바이스 속성 코드입니다. (예: D = 0x0004)</summary>
    public ushort Attribute { get; }

    /// <summary>비트 디바이스 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static YokogawaAddress Parse(string address, bool isBitAccess = false)
    {
        if (!TryParse(address, isBitAccess, out YokogawaAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        bool isBitAccess,
        [NotNullWhen(true)] out YokogawaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (!TryExtractDevice(trimmed, out string devicePrefix, out string numberText))
        {
            errorMessage = $"지원하지 않는 Yokogawa 디바이스 접두사입니다: {address}";
            return false;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < 0)
        {
            errorMessage = $"유효하지 않은 Yokogawa 주소 번호입니다: {address}";
            return false;
        }

        return TryResolveDevice(trimmed, devicePrefix, number, isBitAccess, out result, out errorMessage);
    }

    private static bool TryExtractDevice(string trimmed, out string devicePrefix, out string numberText)
    {
        devicePrefix = string.Empty;
        numberText = string.Empty;

        string[] twoCharPrefixes = ["TS", "TP", "TI", "CS", "CP", "CI"];
        foreach (string d in twoCharPrefixes)
        {
            if (trimmed.StartsWith(d, StringComparison.Ordinal))
            {
                devicePrefix = d;
                numberText = trimmed.Substring(2);
                return true;
            }
        }

        char p1 = trimmed[0];
        devicePrefix = p1.ToString();
        numberText = trimmed.Substring(1);
        return p1 is 'X' or 'Y' or 'I' or 'E' or 'M' or 'T' or 'C' or 'L' or 'D' or 'B' or 'F' or 'R' or 'V' or 'Z' or 'W';
    }

    private static bool TryResolveDevice(
        string original,
        string devicePrefix,
        int number,
        bool isBitAccess,
        [NotNullWhen(true)] out YokogawaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        ushort attribute;
        bool isBit;

        switch (devicePrefix)
        {
            case "X": attribute = 0x0018; isBit = true; break;
            case "Y": attribute = 0x0019; isBit = true; break;
            case "I": attribute = 0x0009; isBit = true; break;
            case "E": attribute = 0x0005; isBit = true; break;
            case "M": attribute = 0x000D; isBit = true; break;
            case "T": attribute = 0x0014; isBit = true; break;
            case "C": attribute = 0x0003; isBit = true; break;
            case "L": attribute = 0x000C; isBit = true; break;

            case "D": attribute = 0x0004; isBit = false; break;
            case "B": attribute = 0x0002; isBit = false; break;
            case "F": attribute = 0x0006; isBit = false; break;
            case "R": attribute = 0x0012; isBit = false; break;
            case "V": attribute = 0x0016; isBit = false; break;
            case "Z": attribute = 0x001A; isBit = false; break;
            case "W": attribute = 0x0017; isBit = false; break;
            case "TS": attribute = 0x0020; isBit = false; break;
            case "TP": attribute = 0x0021; isBit = false; break;
            case "TI": attribute = 0x0025; isBit = false; break;
            case "CS": attribute = 0x0030; isBit = false; break;
            case "CP": attribute = 0x0031; isBit = false; break;
            case "CI": attribute = 0x0035; isBit = false; break;
            default:
                errorMessage = $"알 수 없는 Yokogawa 디바이스입니다: {devicePrefix}";
                return false;
        }

        if (isBitAccess)
        {
            isBit = true;
        }

        result = new YokogawaAddress(original, devicePrefix, number, attribute, isBit);
        return true;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="YokogawaAddress"/>를 반환합니다.</summary>
    public YokogawaAddress Offset(int count)
    {
        return new YokogawaAddress($"{Original}+{count}", Device, Number + count, Attribute, IsBit);
    }
}
