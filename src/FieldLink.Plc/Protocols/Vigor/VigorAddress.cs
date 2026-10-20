using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 PLC 디바이스 주소 표현입니다. (사양서 §1.5, §1.6)</summary>
public sealed class VigorAddress
{
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
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (!TryExtractDevice(trimmed, out string devicePrefix, out string numberText))
        {
            errorMessage = $"지원하지 않는 VIGOR 디바이스 접두사입니다: {address}";
            return false;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < 0 || number > 999999)
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

        string[] twoCharPrefixes = ["SM", "SD", "TC", "TS", "CC", "CS"];
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
        return p1 is 'X' or 'Y' or 'M' or 'S' or 'D' or 'R' or 'T' or 'C';
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
                deviceCode = 0x90;
                isBit = true;
                break;
            case "Y":
                deviceCode = 0x91;
                isBit = true;
                break;
            case "M":
                if (number >= 9000)
                {
                    deviceCode = 0x94;
                    number -= 9000;
                }
                else
                {
                    deviceCode = 0x92;
                }
                isBit = true;
                break;
            case "S":
                deviceCode = 0x93;
                isBit = true;
                break;
            case "SM":
                deviceCode = 0x94;
                isBit = true;
                break;
            case "TC":
                deviceCode = 0x98;
                isBit = true;
                break;
            case "TS":
                deviceCode = 0x99;
                isBit = true;
                break;
            case "CC":
                deviceCode = 0x9C;
                isBit = true;
                break;
            case "CS":
                deviceCode = 0x9D;
                isBit = true;
                break;
            case "D":
                if (number >= 9000)
                {
                    deviceCode = 0xA1;
                    number -= 9000;
                }
                else
                {
                    deviceCode = 0xA0;
                }
                break;
            case "SD":
                deviceCode = 0xA1;
                break;
            case "R":
                deviceCode = 0xA2;
                break;
            case "T":
                deviceCode = 0xA8;
                break;
            case "C":
                if (number >= 200)
                {
                    deviceCode = 0xAD;
                    is32Bit = true;
                }
                else
                {
                    deviceCode = 0xAC;
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
        int d0 = number % 10;
        int d1 = (number / 10) % 10;
        int d2 = (number / 100) % 10;
        int d3 = (number / 1000) % 10;
        int d4 = (number / 10000) % 10;
        int d5 = (number / 100000) % 10;

        byte b0 = (byte)((d1 << 4) | d0);
        byte b1 = (byte)((d3 << 4) | d2);
        byte b2 = (byte)((d5 << 4) | d4);

        return [b0, b1, b2];
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="VigorAddress"/>를 반환합니다.</summary>
    public VigorAddress Offset(int count)
    {
        int nextNum = Number + count;
        byte[] nextBcd = EncodeBcdLe(nextNum);
        return new VigorAddress($"{Original}+{count}", Device, nextNum, DeviceCode, nextBcd, IsBit, Is32BitWord);
    }
}
