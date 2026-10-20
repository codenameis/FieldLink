using System;
using System.Diagnostics.CodeAnalysis;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>Toyota / JTEKT TOYOPUC Computer Link 2PORT-EFR 주소 표현입니다. (사양서 §1.2~§1.6)</summary>
public sealed class ToyopucAddress
{
    private ToyopucAddress(string original, string device, int number, ushort wireAddress, byte? prg, bool isBit)
    {
        Original = original;
        Device = device;
        Number = number;
        WireAddress = wireAddress;
        Prg = prg;
        IsBit = isBit;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>디바이스 접두사입니다. (예: "D", "M", "K", "EK", "EB", "H")</summary>
    public string Device { get; }

    /// <summary>16진 번호입니다.</summary>
    public int Number { get; }

    /// <summary>TOYOPUC 와이어 16비트 주소입니다.</summary>
    public ushort WireAddress { get; }

    /// <summary>확장 영역 프로그램 번호(Ex No.)입니다. 기본 영역이면 null입니다.</summary>
    public byte? Prg { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static ToyopucAddress Parse(string address, bool isBitAccess = false)
    {
        if (!TryParse(address, isBitAccess, out ToyopucAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        bool isBitAccess,
        [NotNullWhen(true)] out ToyopucAddress? result,
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
            errorMessage = $"지원하지 않는 TOYOPUC 디바이스 접두사입니다: {address}";
            return false;
        }

        if (!int.TryParse(numberText, System.Globalization.NumberStyles.HexNumber, null, out int hexNum) || hexNum < 0)
        {
            errorMessage = $"16진 주소 파싱 실패: {address}";
            return false;
        }

        return TryResolveAddress(trimmed, devicePrefix, hexNum, isBitAccess, out result, out errorMessage);
    }

    private static bool TryExtractDevice(string trimmed, out string devicePrefix, out string numberText)
    {
        devicePrefix = string.Empty;
        numberText = string.Empty;

        string[] twoCharPrefixes = ["EK", "EV", "ET", "EC", "EL", "EX", "EY", "EM", "ES", "EN", "EB", "GX", "GY", "GM"];
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
        return p1 is 'K' or 'V' or 'T' or 'C' or 'L' or 'X' or 'Y' or 'M' or 'S' or 'N' or 'R' or 'D' or 'B' or 'H' or 'U';
    }

    private static bool TryResolveAddress(
        string original,
        string devicePrefix,
        int hexNum,
        bool isBitAccess,
        [NotNullWhen(true)] out ToyopucAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (devicePrefix.StartsWith("E", StringComparison.Ordinal) || devicePrefix is "H" or "U" || devicePrefix.StartsWith("G", StringComparison.Ordinal))
        {
            return TryResolveExtended(original, devicePrefix, hexNum, isBitAccess, out result, out errorMessage);
        }

        return TryResolveBasic(original, devicePrefix, hexNum, isBitAccess, out result, out errorMessage);
    }

    private static bool TryResolveBasic(
        string original,
        string devicePrefix,
        int hexNum,
        bool isBitAccess,
        [NotNullWhen(true)] out ToyopucAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        bool isBit = isBitAccess;
        ushort baseOffset;
        if (isBit)
        {
            baseOffset = devicePrefix switch
            {
                "K" => 0x0200,
                "V" => 0x0500,
                "T" or "C" => 0x0600,
                "L" => 0x0800,
                "X" or "Y" => 0x1000,
                "M" => 0x1800,
                _ => 0,
            };
        }
        else
        {
            baseOffset = devicePrefix switch
            {
                "K" => 0x0020,
                "V" => 0x0050,
                "T" or "C" => 0x0060,
                "L" => 0x0080,
                "X" or "Y" => 0x0100,
                "M" => 0x0180,
                "S" => 0x0200,
                "N" => 0x0600,
                "R" => 0x0800,
                "D" => 0x1000,
                "B" => 0x6000,
                _ => 0,
            };
        }

        ushort wire = (ushort)(baseOffset + hexNum);
        result = new ToyopucAddress(original, devicePrefix, hexNum, wire, prg: null, isBit);
        return true;
    }

    private static bool TryResolveExtended(
        string original,
        string devicePrefix,
        int hexNum,
        bool isBitAccess,
        [NotNullWhen(true)] out ToyopucAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (devicePrefix == "EB")
        {
            byte prg = hexNum < 0x8000 ? (byte)0x09 : hexNum < 0x10000 ? (byte)0x0A : (byte)0x0B;
            int offsetInBank = hexNum < 0x8000 ? hexNum : hexNum < 0x10000 ? hexNum - 0x8000 : hexNum - 0x10000;
            result = new ToyopucAddress(original, devicePrefix, hexNum, (ushort)offsetInBank, prg, isBit: false);
            return true;
        }

        byte extPrg = devicePrefix switch
        {
            "H" or "EK" or "EV" or "ET" or "EC" or "EL" or "EX" or "EY" or "EM" or "ES" or "EN" => 0x00,
            "GX" or "GY" or "GM" => 0x07,
            "U" => 0x08,
            _ => 0xFF,
        };

        ushort baseWord = devicePrefix switch
        {
            "EK" => 0x0100,
            "EV" => 0x0200,
            "ET" or "EC" => 0x0300,
            "EL" => 0x0380,
            "EX" or "EY" => 0x0580,
            "EM" => 0x0600,
            "ES" => 0x0800,
            "EN" => 0x1000,
            "H" => 0x1800,
            "GX" or "GY" or "U" => 0x0000,
            "GM" => 0x1000,
            _ => 0x0000,
        };

        ushort wire = (ushort)(baseWord + hexNum);
        result = new ToyopucAddress(original, devicePrefix, hexNum, wire, extPrg, isBit: isBitAccess);
        return true;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="ToyopucAddress"/>를 반환합니다.</summary>
    public ToyopucAddress Offset(int count)
    {
        int nextNum = Number + count;
        ushort nextWire = (ushort)(WireAddress + count);
        return new ToyopucAddress($"{Original}+{count}", Device, nextNum, nextWire, Prg, IsBit);
    }
}
