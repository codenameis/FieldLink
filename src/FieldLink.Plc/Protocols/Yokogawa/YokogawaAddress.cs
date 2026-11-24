using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 higher-level link service 디바이스 주소 표현입니다. (사양서 §1.3)</summary>
public sealed class YokogawaAddress
{
    private const int SingleCharPrefixLength = 1;
    private const int TwoCharPrefixLength = 2;

    private const ushort AttributeX = 0x0018;
    private const ushort AttributeY = 0x0019;
    private const ushort AttributeI = 0x0009;
    private const ushort AttributeE = 0x0005;
    private const ushort AttributeM = 0x000D;
    private const ushort AttributeT = 0x0014;
    private const ushort AttributeC = 0x0003;
    private const ushort AttributeL = 0x000C;
    private const ushort AttributeD = 0x0004;
    private const ushort AttributeB = 0x0002;
    private const ushort AttributeF = 0x0006;
    private const ushort AttributeR = 0x0012;
    private const ushort AttributeV = 0x0016;
    private const ushort AttributeZ = 0x001A;
    private const ushort AttributeW = 0x0017;
    private const ushort AttributeTimerSet = 0x0020;
    private const ushort AttributeTimerPresent = 0x0021;
    private const ushort AttributeTimerIntegration = 0x0025;
    private const ushort AttributeCounterSet = 0x0030;
    private const ushort AttributeCounterPresent = 0x0031;
    private const ushort AttributeCounterIntegration = 0x0035;

    private static readonly string[] TwoCharPrefixes = ["TS", "TP", "TI", "CS", "CP", "CI"];

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
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

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
            errorMessage = "주소 문자열은 필수입니다.";
            return false;
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

        foreach (string prefix in TwoCharPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                devicePrefix = prefix;
                numberText = trimmed.Substring(TwoCharPrefixLength);
                return numberText.Length > 0;
            }
        }

        char p1 = trimmed[0];
        devicePrefix = p1.ToString();
        numberText = trimmed.Substring(SingleCharPrefixLength);
        return numberText.Length > 0
            && p1 is 'X' or 'Y' or 'I' or 'E' or 'M' or 'T' or 'C' or 'L' or 'D' or 'B' or 'F' or 'R' or 'V' or 'Z' or 'W';
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
            case "X": attribute = AttributeX; isBit = true; break;
            case "Y": attribute = AttributeY; isBit = true; break;
            case "I": attribute = AttributeI; isBit = true; break;
            case "E": attribute = AttributeE; isBit = true; break;
            case "M": attribute = AttributeM; isBit = true; break;
            case "T": attribute = AttributeT; isBit = true; break;
            case "C": attribute = AttributeC; isBit = true; break;
            case "L": attribute = AttributeL; isBit = true; break;

            case "D": attribute = AttributeD; isBit = false; break;
            case "B": attribute = AttributeB; isBit = false; break;
            case "F": attribute = AttributeF; isBit = false; break;
            case "R": attribute = AttributeR; isBit = false; break;
            case "V": attribute = AttributeV; isBit = false; break;
            case "Z": attribute = AttributeZ; isBit = false; break;
            case "W": attribute = AttributeW; isBit = false; break;
            case "TS": attribute = AttributeTimerSet; isBit = false; break;
            case "TP": attribute = AttributeTimerPresent; isBit = false; break;
            case "TI": attribute = AttributeTimerIntegration; isBit = false; break;
            case "CS": attribute = AttributeCounterSet; isBit = false; break;
            case "CP": attribute = AttributeCounterPresent; isBit = false; break;
            case "CI": attribute = AttributeCounterIntegration; isBit = false; break;
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
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        return new YokogawaAddress($"{Original}+{count}", Device, checked(Number + count), Attribute, IsBit);
    }
}
