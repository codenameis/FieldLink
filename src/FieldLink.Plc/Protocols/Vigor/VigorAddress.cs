using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 PLC 디바이스 주소 표현입니다. (사양서 §1.5, §1.6)</summary>
public sealed class VigorAddress
{
    private const int MaxBcdNumber = 999999;
    private const int SpecialDeviceBaseNumber = 9000;
    private const int Counter32BitStartNumber = 200;
    private const int SingleCharPrefixLength = 1;
    private const int TwoCharPrefixLength = 2;
    private const int DecimalBase = 10;
    private const int BcdHighNibbleShift = 4;

    private const byte DeviceCodeX = 0x90;
    private const byte DeviceCodeY = 0x91;
    private const byte DeviceCodeM = 0x92;
    private const byte DeviceCodeS = 0x93;
    private const byte DeviceCodeSpecialM = 0x94;
    private const byte DeviceCodeTimerContact = 0x98;
    private const byte DeviceCodeTimerCoil = 0x99;
    private const byte DeviceCodeCounterContact = 0x9C;
    private const byte DeviceCodeCounterCoil = 0x9D;
    private const byte DeviceCodeD = 0xA0;
    private const byte DeviceCodeSpecialD = 0xA1;
    private const byte DeviceCodeR = 0xA2;
    private const byte DeviceCodeT = 0xA8;
    private const byte DeviceCodeCounter16Bit = 0xAC;
    private const byte DeviceCodeCounter32Bit = 0xAD;

    private static readonly string[] TwoCharPrefixes = ["SM", "SD", "TC", "TS", "CC", "CS"];

    private VigorAddress(string original, string device, int number, byte deviceCode, byte[] bcdAddress, bool isBit, bool is32BitWord)
    {
        Original = original;
        Device = device;
        Number = number;
        DeviceCode = deviceCode;
        BcdAddress = bcdAddress;
        IsBit = isBit;
        Is32BitWord = is32BitWord;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>디바이스 접두사입니다. (예: "D", "M", "X", "SD", "SM")</summary>
    public string Device { get; }

    /// <summary>디바이스 번호입니다.</summary>
    public int Number { get; }

    /// <summary>VIGOR 1바이트 디바이스 코드입니다. (예: D = 0xA0, M = 0x92)</summary>
    public byte DeviceCode { get; }

    /// <summary>3바이트 BCD 리틀엔디언 주소 인코딩입니다.</summary>
    public byte[] BcdAddress { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>32비트 고속 카운터(C200~C255) 여부입니다.</summary>
    public bool Is32BitWord { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static VigorAddress Parse(string address, bool isBitAccess = false)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!TryParse(address, isBitAccess, out VigorAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        bool isBitAccess,
        [NotNullWhen(true)] out VigorAddress? result,
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
            errorMessage = $"지원하지 않는 VIGOR 디바이스 접두사입니다: {address}";
            return false;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            || number < 0
            || number > MaxBcdNumber)
        {
            errorMessage = $"VIGOR 주소 번호가 범위를 초과했거나 유효하지 않습니다: {address}";
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
        return numberText.Length > 0 && p1 is 'X' or 'Y' or 'M' or 'S' or 'D' or 'R' or 'T' or 'C';
    }

    private static bool TryResolveDevice(
        string original,
        string devicePrefix,
        int number,
        bool isBitAccess,
        [NotNullWhen(true)] out VigorAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        byte deviceCode;
        bool isBit = isBitAccess;
        bool is32Bit = false;

        switch (devicePrefix)
        {
            case "X":
                deviceCode = DeviceCodeX;
                isBit = true;
                break;
            case "Y":
                deviceCode = DeviceCodeY;
                isBit = true;
                break;
            case "M":
                if (number >= SpecialDeviceBaseNumber)
                {
                    deviceCode = DeviceCodeSpecialM;
                    number -= SpecialDeviceBaseNumber;
                }
                else
                {
                    deviceCode = DeviceCodeM;
                }
                isBit = true;
                break;
            case "S":
                deviceCode = DeviceCodeS;
                isBit = true;
                break;
            case "SM":
                deviceCode = DeviceCodeSpecialM;
                isBit = true;
                break;
            case "TC":
                deviceCode = DeviceCodeTimerContact;
                isBit = true;
                break;
            case "TS":
                deviceCode = DeviceCodeTimerCoil;
                isBit = true;
                break;
            case "CC":
                deviceCode = DeviceCodeCounterContact;
                isBit = true;
                break;
            case "CS":
                deviceCode = DeviceCodeCounterCoil;
                isBit = true;
                break;
            case "D":
                if (number >= SpecialDeviceBaseNumber)
                {
                    deviceCode = DeviceCodeSpecialD;
                    number -= SpecialDeviceBaseNumber;
                }
                else
                {
                    deviceCode = DeviceCodeD;
                }
                break;
            case "SD":
                deviceCode = DeviceCodeSpecialD;
                break;
            case "R":
                deviceCode = DeviceCodeR;
                break;
            case "T":
                deviceCode = DeviceCodeT;
                break;
            case "C":
                if (number >= Counter32BitStartNumber)
                {
                    deviceCode = DeviceCodeCounter32Bit;
                    is32Bit = true;
                }
                else
                {
                    deviceCode = DeviceCodeCounter16Bit;
                }
                break;
            default:
                errorMessage = $"알 수 없는 디바이스 접두사입니다: {devicePrefix}";
                return false;
        }

        byte[] bcd = EncodeBcdLe(number);
        result = new VigorAddress(original, devicePrefix, number, deviceCode, bcd, isBit, is32Bit);
        return true;
    }

    /// <summary>10진수 번호를 6자리 3바이트 BCD 리틀엔디언으로 인코딩합니다. (§1.6)</summary>
    public static byte[] EncodeBcdLe(int number)
    {
        if (number < 0 || number > MaxBcdNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, $"BCD 주소 번호는 0~{MaxBcdNumber} 범위여야 합니다.");
        }

        int remaining = number;
        int d0 = remaining % DecimalBase;
        remaining /= DecimalBase;
        int d1 = remaining % DecimalBase;
        remaining /= DecimalBase;
        int d2 = remaining % DecimalBase;
        remaining /= DecimalBase;
        int d3 = remaining % DecimalBase;
        remaining /= DecimalBase;
        int d4 = remaining % DecimalBase;
        remaining /= DecimalBase;
        int d5 = remaining % DecimalBase;

        byte b0 = (byte)((d1 << BcdHighNibbleShift) | d0);
        byte b1 = (byte)((d3 << BcdHighNibbleShift) | d2);
        byte b2 = (byte)((d5 << BcdHighNibbleShift) | d4);

        return [b0, b1, b2];
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="VigorAddress"/>를 반환합니다.</summary>
    public VigorAddress Offset(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        int nextNum = checked(Number + count);
        byte[] nextBcd = EncodeBcdLe(nextNum);
        return new VigorAddress($"{Original}+{count}", Device, nextNum, DeviceCode, nextBcd, IsBit, Is32BitWord);
    }
}
