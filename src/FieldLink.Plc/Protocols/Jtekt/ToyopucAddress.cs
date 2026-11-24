using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>Toyota / JTEKT TOYOPUC Computer Link 2PORT-EFR 주소 표현입니다. (사양서 §1.2~§1.6)</summary>
public sealed class ToyopucAddress
{
    private const int TwoCharPrefixLength = 2;
    private const int SingleCharPrefixLength = 1;
    private const int EbBankSize = 0x8000;
    private const int EbBank2Start = 0x10000;
    private const int EbMaxAddress = 0x18000;
    private const byte EbBank0Prg = 0x09;
    private const byte EbBank1Prg = 0x0A;
    private const byte EbBank2Prg = 0x0B;
    private const byte ExtendedExPrg = 0x00;
    private const byte ExtendedGxPrg = 0x07;
    private const byte ExtendedUPrg = 0x08;

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
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

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

        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            errorMessage = "주소 문자열이 비어 있습니다.";
            return false;
        }

        string trimmed = address.Trim().ToUpperInvariant();
        if (!TryExtractDevice(trimmed, out string devicePrefix, out string numberText))
        {
            errorMessage = $"지원하지 않는 TOYOPUC 디바이스 접두사입니다: {address}";
            return false;
        }

        if (!int.TryParse(numberText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexNum) || hexNum < 0)
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
                numberText = trimmed.Substring(TwoCharPrefixLength);
                return true;
            }
        }

        char p1 = trimmed[0];
        devicePrefix = p1.ToString();
        numberText = trimmed.Substring(SingleCharPrefixLength);
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
        int baseOffset;
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
                _ => -1,
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
                _ => -1,
            };
        }

        if (baseOffset < 0 || baseOffset + (long)hexNum > ushort.MaxValue)
        {
            errorMessage = isBit
                ? $"TOYOPUC '{devicePrefix}' 디바이스는 비트 접근을 지원하지 않거나 주소 범위를 초과했습니다: {original}"
                : $"TOYOPUC 기본 주소 범위를 초과했습니다: {original}";
            return false;
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
            if (isBitAccess || hexNum >= EbMaxAddress)
            {
                errorMessage = $"TOYOPUC EB 주소가 유효 범위를 벗어났습니다: {original}";
                return false;
            }

            byte prg = hexNum < EbBankSize ? EbBank0Prg : hexNum < EbBank2Start ? EbBank1Prg : EbBank2Prg;
            int offsetInBank = hexNum < EbBankSize ? hexNum : hexNum < EbBank2Start ? hexNum - EbBankSize : hexNum - EbBank2Start;
            result = new ToyopucAddress(original, devicePrefix, hexNum, (ushort)offsetInBank, prg, isBit: false);
            return true;
        }

        byte extPrg = devicePrefix switch
        {
            "H" or "EK" or "EV" or "ET" or "EC" or "EL" or "EX" or "EY" or "EM" or "ES" or "EN" => ExtendedExPrg,
            "GX" or "GY" or "GM" => ExtendedGxPrg,
            "U" => ExtendedUPrg,
            _ => byte.MaxValue,
        };

        if (extPrg == byte.MaxValue)
        {
            errorMessage = $"지원하지 않는 TOYOPUC 확장 디바이스입니다: {original}";
            return false;
        }

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

        if (baseWord + (long)hexNum > ushort.MaxValue)
        {
            errorMessage = $"TOYOPUC 확장 주소가 16비트 범위를 초과했습니다: {original}";
            return false;
        }

        ushort wire = (ushort)(baseWord + hexNum);
        result = new ToyopucAddress(original, devicePrefix, hexNum, wire, extPrg, isBit: isBitAccess);
        return true;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="ToyopucAddress"/>를 반환합니다.</summary>
    public ToyopucAddress Offset(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        if (count == 0)
        {
            return this;
        }

        long nextNumLong = (long)Number + count;
        if (Device == "EB")
        {
            if (nextNumLong >= EbMaxAddress)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "TOYOPUC EB 주소가 최대 뱅크 범위를 초과합니다.");
            }

            int nextEbNum = (int)nextNumLong;
            byte nextPrg = nextEbNum < EbBankSize ? EbBank0Prg : nextEbNum < EbBank2Start ? EbBank1Prg : EbBank2Prg;
            int offsetInBank = nextEbNum < EbBankSize ? nextEbNum : nextEbNum < EbBank2Start ? nextEbNum - EbBankSize : nextEbNum - EbBank2Start;
            return new ToyopucAddress($"{Original}+{count}", Device, nextEbNum, (ushort)offsetInBank, nextPrg, IsBit);
        }

        if (WireAddress + (long)count > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "TOYOPUC 주소가 16비트 와이어 범위를 초과합니다.");
        }

        int nextNum = (int)nextNumLong;
        ushort nextWire = (ushort)(WireAddress + count);
        return new ToyopucAddress($"{Original}+{count}", Device, nextNum, nextWire, Prg, IsBit);
    }
}
