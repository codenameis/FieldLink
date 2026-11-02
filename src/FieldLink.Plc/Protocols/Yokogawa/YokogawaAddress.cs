using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 higher-level link service 디바이스 주소 표현입니다. (사양서 §1.3)</summary>
public sealed class YokogawaAddress
{
    private const ushort AttributeInputRelayX = 0x0018;
    private const ushort AttributeOutputRelayY = 0x0019;
    private const ushort AttributeInternalRelayI = 0x0009;
    private const ushort AttributeSharedRelayE = 0x0005;
    private const ushort AttributeSpecialRelayM = 0x000D;
    private const ushort AttributeTimerRelayT = 0x0014;
    private const ushort AttributeCounterRelayC = 0x0003;
    private const ushort AttributeLinkRelayL = 0x000C;

    private const ushort AttributeDataRegisterD = 0x0004;
    private const ushort AttributeFileRegisterB = 0x0002;
    private const ushort AttributeCacheRegisterF = 0x0006;
    private const ushort AttributeSharedRegisterR = 0x0012;
    private const ushort AttributeIndexRegisterV = 0x0016;
    private const ushort AttributeSpecialRegisterZ = 0x001A;
    private const ushort AttributeLinkRegisterW = 0x0017;
    private const ushort AttributeTimerSetValueTs = 0x0020;
    private const ushort AttributeTimerPresentValueTp = 0x0021;
    private const ushort AttributeTimerCountUpValueTi = 0x0025;
    private const ushort AttributeCounterSetValueCs = 0x0030;
    private const ushort AttributeCounterPresentValueCp = 0x0031;
    private const ushort AttributeCounterCountUpValueCi = 0x0035;

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
    public static YokogawaAddress Parse(string address)
    {
        if (!TryParse(address, out YokogawaAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
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

        return TryResolveDevice(trimmed, devicePrefix, number, out result, out errorMessage);
    }

    private static bool TryExtractDevice(string trimmed, out string devicePrefix, out string numberText)
    {
        foreach (string prefixCandidate in TwoCharPrefixes)
        {
            if (trimmed.StartsWith(prefixCandidate, StringComparison.Ordinal))
            {
                devicePrefix = prefixCandidate;
                numberText = trimmed.Substring(prefixCandidate.Length);
                return true;
            }
        }

        char firstChar = trimmed[0];
        devicePrefix = firstChar.ToString();
        numberText = trimmed.Substring(1);
        return firstChar is 'X' or 'Y' or 'I' or 'E' or 'M' or 'T' or 'C' or 'L' or 'D' or 'B' or 'F' or 'R' or 'V' or 'Z' or 'W';
    }

    private static bool TryResolveDevice(
        string original,
        string devicePrefix,
        int number,
        [NotNullWhen(true)] out YokogawaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (TryResolveBitAttribute(devicePrefix, out ushort bitAttribute))
        {
            result = new YokogawaAddress(original, devicePrefix, number, bitAttribute, isBit: true);
            return true;
        }

        if (TryResolveWordAttribute(devicePrefix, out ushort wordAttribute))
        {
            result = new YokogawaAddress(original, devicePrefix, number, wordAttribute, isBit: false);
            return true;
        }

        errorMessage = $"알 수 없는 Yokogawa 디바이스입니다: {devicePrefix}";
        return false;
    }

    private static bool TryResolveBitAttribute(string devicePrefix, out ushort attribute)
    {
        attribute = devicePrefix switch
        {
            "X" => AttributeInputRelayX,
            "Y" => AttributeOutputRelayY,
            "I" => AttributeInternalRelayI,
            "E" => AttributeSharedRelayE,
            "M" => AttributeSpecialRelayM,
            "T" => AttributeTimerRelayT,
            "C" => AttributeCounterRelayC,
            "L" => AttributeLinkRelayL,
            _ => 0,
        };

        return attribute != 0;
    }

    private static bool TryResolveWordAttribute(string devicePrefix, out ushort attribute)
    {
        attribute = devicePrefix switch
        {
            "D" => AttributeDataRegisterD,
            "B" => AttributeFileRegisterB,
            "F" => AttributeCacheRegisterF,
            "R" => AttributeSharedRegisterR,
            "V" => AttributeIndexRegisterV,
            "Z" => AttributeSpecialRegisterZ,
            "W" => AttributeLinkRegisterW,
            "TS" => AttributeTimerSetValueTs,
            "TP" => AttributeTimerPresentValueTp,
            "TI" => AttributeTimerCountUpValueTi,
            "CS" => AttributeCounterSetValueCs,
            "CP" => AttributeCounterPresentValueCp,
            "CI" => AttributeCounterCountUpValueCi,
            _ => 0,
        };

        return attribute != 0;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="YokogawaAddress"/>를 반환합니다.</summary>
    public YokogawaAddress Offset(int count)
    {
        return new YokogawaAddress($"{Original}+{count}", Device, Number + count, Attribute, IsBit);
    }
}
