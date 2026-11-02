using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Jtekt;

/// <summary>Toyota / JTEKT TOYOPUC Computer Link 2PORT-EFR 주소 표현입니다. (사양서 §1.2~§1.6)</summary>
public sealed class ToyopucAddress
{
    private const int BitsPerWord = 16;
    private const int MaxBitOffset = 15;
    private const int ExtendedBank1Limit = 0x8000;
    private const int ExtendedBank2Limit = 0x10000;
    private const int MaxExtendedFileRegisterAddress = 0x3FFFF;
    private const byte ExtendedProgramNumber0 = 0x00;
    private const byte ExtendedProgramNumberGlobal = 0x07;
    private const byte ExtendedProgramNumberBuffer = 0x08;
    private const byte ExtendedProgramNumberBank0 = 0x09;
    private const byte ExtendedProgramNumberBank1 = 0x0A;
    private const byte ExtendedProgramNumberBank2 = 0x0B;

    private static readonly string[] TwoCharPrefixes =
    [
        "EK", "EV", "ET", "EC", "EL", "EX", "EY", "EM", "ES", "EN", "EB", "GX", "GY", "GM"
    ];

    private ToyopucAddress(
        string original,
        string device,
        int number,
        ushort wireAddress,
        byte? prg,
        bool isBit,
        byte bitOffset)
    {
        Original = original;
        Device = device;
        Number = number;
        WireAddress = wireAddress;
        Prg = prg;
        IsBit = isBit;
        BitOffset = bitOffset;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>디바이스 접두사입니다. (예: "D", "M", "K", "EK", "EB", "H")</summary>
    public string Device { get; }

    /// <summary>16진 오프셋 번호입니다.</summary>
    public int Number { get; }

    /// <summary>TOYOPUC 와이어 16비트 주소입니다. (확장 비트 주소일 때는 대상 워드의 와이어 주소입니다)</summary>
    public ushort WireAddress { get; }

    /// <summary>확장 영역 프로그램 번호(Ex No.)입니다. 기본 영역이면 null입니다.</summary>
    public byte? Prg { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>워드 내 비트 오프셋(0~15)입니다. 워드 접근일 때는 0입니다.</summary>
    public byte BitOffset { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    /// <param name="address">파싱할 주소 문자열입니다.</param>
    /// <param name="isBitAccess">비트 단위 접근 여부입니다.</param>
    /// <returns>해석된 <see cref="ToyopucAddress"/> 인스턴스입니다.</returns>
    public static ToyopucAddress Parse(string address, bool isBitAccess = false)
    {
        if (!TryParse(address, isBitAccess, out ToyopucAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    /// <param name="address">파싱할 주소 문자열입니다.</param>
    /// <param name="isBitAccess">비트 단위 접근 여부입니다.</param>
    /// <param name="result">파싱에 성공한 경우 결과 주소 객체입니다.</param>
    /// <param name="errorMessage">파싱에 실패한 경우 오류 사유입니다.</param>
    /// <returns>파싱 성공 여부입니다.</returns>
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

        if (!TryParseNumberAndBit(numberText, isBitAccess, out int hexNumber, out byte bitOffset))
        {
            errorMessage = $"16진 주소 파싱 실패: {address}";
            return false;
        }

        return TryResolveAddress(trimmed, devicePrefix, hexNumber, bitOffset, isBitAccess, out result, out errorMessage);
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="ToyopucAddress"/>를 반환합니다.</summary>
    /// <param name="elementOffset">이동할 오프셋(비트 주소면 비트 수, 워드 주소면 워드 수)입니다.</param>
    /// <returns>오프셋이 적용된 새 주소입니다.</returns>
    public ToyopucAddress Offset(int elementOffset)
    {
        if (Device == "EB" && Prg.HasValue)
        {
            int linearAddress = ((Prg.Value - ExtendedProgramNumberBank0) * ExtendedBank1Limit) + WireAddress + elementOffset;
            if (linearAddress is < 0 or > MaxExtendedFileRegisterAddress)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elementOffset),
                    elementOffset,
                    $"EB 확장 파일 레지스터 주소 범위(0x00000~0x3FFFF)를 벗어났습니다: 0x{linearAddress:X}");
            }

            ResolveExtendedFileRegisterBank(linearAddress, out byte nextPrg, out ushort nextBankWire);
            return new ToyopucAddress($"{Original}+{elementOffset}", Device, linearAddress, nextBankWire, nextPrg, isBit: false, bitOffset: 0);
        }

        if (IsBit && Prg.HasValue)
        {
            int nextBitNumber = Number + elementOffset;
            int totalBitOffset = BitOffset + elementOffset;
            int wordDelta = totalBitOffset / BitsPerWord;
            byte nextBitOffset = (byte)(totalBitOffset % BitsPerWord);
            ushort nextWordWire = (ushort)(WireAddress + wordDelta);
            return new ToyopucAddress($"{Original}+{elementOffset}", Device, nextBitNumber, nextWordWire, Prg, isBit: true, nextBitOffset);
        }

        int nextNumber = Number + elementOffset;
        ushort nextWireAddress = (ushort)(WireAddress + elementOffset);
        byte nextBasicBitOffset = IsBit ? (byte)((BitOffset + elementOffset) & MaxBitOffset) : (byte)0;
        return new ToyopucAddress($"{Original}+{elementOffset}", Device, nextNumber, nextWireAddress, Prg, IsBit, nextBasicBitOffset);
    }

    /// <summary>확장 비트 주소의 와이어 워드 주소에서 지정한 워드 수만큼 이동한 워드 주소를 반환합니다.</summary>
    /// <param name="wordOffset">이동할 워드 오프셋입니다.</param>
    /// <returns>워드 단위로 이동된 새 주소입니다.</returns>
    public ToyopucAddress OffsetWords(int wordOffset)
    {
        ushort nextWordWire = (ushort)(WireAddress + wordOffset);
        return new ToyopucAddress($"{Original}+W{wordOffset}", Device, Number + (wordOffset * BitsPerWord), nextWordWire, Prg, isBit: false, bitOffset: 0);
    }

    private static bool TryExtractDevice(string trimmed, out string devicePrefix, out string numberText)
    {
        devicePrefix = string.Empty;
        numberText = string.Empty;

        foreach (string candidate in TwoCharPrefixes)
        {
            if (trimmed.StartsWith(candidate, StringComparison.Ordinal))
            {
                devicePrefix = candidate;
                numberText = trimmed.Substring(2);
                return numberText.Length > 0;
            }
        }

        char firstChar = trimmed[0];
        devicePrefix = firstChar.ToString();
        numberText = trimmed.Substring(1);
        bool isValidPrefix = firstChar is 'K' or 'V' or 'T' or 'C' or 'L' or 'X' or 'Y' or 'M' or 'S' or 'N' or 'R' or 'D' or 'B' or 'H' or 'U';
        return isValidPrefix && numberText.Length > 0;
    }

    private static bool TryParseNumberAndBit(string numberText, bool isBitAccess, out int hexNumber, out byte bitOffset)
    {
        hexNumber = 0;
        bitOffset = 0;

        int dotIndex = numberText.IndexOf('.');
        if (dotIndex >= 0)
        {
            if (!isBitAccess)
            {
                return false;
            }

            string wordPart = numberText.Substring(0, dotIndex);
            string bitPart = numberText.Substring(dotIndex + 1);
            if (!int.TryParse(wordPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int wordIndex) || wordIndex < 0)
            {
                return false;
            }

            if (!byte.TryParse(bitPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bitOffset) || bitOffset > MaxBitOffset)
            {
                return false;
            }

            hexNumber = (wordIndex * BitsPerWord) + bitOffset;
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hexNumber) || hexNumber < 0)
        {
            return false;
        }

        if (isBitAccess)
        {
            bitOffset = (byte)(hexNumber & MaxBitOffset);
        }

        return true;
    }

    private static bool TryResolveAddress(
        string original,
        string devicePrefix,
        int hexNumber,
        byte bitOffset,
        bool isBitAccess,
        [NotNullWhen(true)] out ToyopucAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        bool isExtended = devicePrefix.StartsWith("E", StringComparison.Ordinal)
            || devicePrefix is "H" or "U"
            || devicePrefix.StartsWith("G", StringComparison.Ordinal);

        if (isExtended)
        {
            return TryResolveExtended(original, devicePrefix, hexNumber, bitOffset, isBitAccess, out result, out errorMessage);
        }

        return TryResolveBasic(original, devicePrefix, hexNumber, bitOffset, isBitAccess, out result, out errorMessage);
    }

    private static bool TryResolveBasic(
        string original,
        string devicePrefix,
        int hexNumber,
        byte bitOffset,
        bool isBitAccess,
        [NotNullWhen(true)] out ToyopucAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        ushort baseOffset = isBitAccess
            ? GetBasicBitBaseOffset(devicePrefix)
            : GetBasicWordBaseOffset(devicePrefix);

        if (baseOffset == 0)
        {
            errorMessage = $"기본 영역 디바이스 '{devicePrefix}'는 {(isBitAccess ? "비트" : "워드")} 접근을 지원하지 않습니다: {original}";
            return false;
        }

        ushort wireAddress = (ushort)(baseOffset + hexNumber);
        result = new ToyopucAddress(original, devicePrefix, hexNumber, wireAddress, prg: null, isBitAccess, bitOffset);
        return true;
    }

    private static ushort GetBasicBitBaseOffset(string devicePrefix) => devicePrefix switch
    {
        "K" => 0x0200,
        "V" => 0x0500,
        "T" or "C" => 0x0600,
        "L" => 0x0800,
        "X" or "Y" => 0x1000,
        "M" => 0x1800,
        _ => 0,
    };

    private static ushort GetBasicWordBaseOffset(string devicePrefix) => devicePrefix switch
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

    private static bool TryResolveExtended(
        string original,
        string devicePrefix,
        int hexNumber,
        byte bitOffset,
        bool isBitAccess,
        [NotNullWhen(true)] out ToyopucAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (devicePrefix == "EB")
        {
            return TryResolveExtendedFileRegister(original, devicePrefix, hexNumber, isBitAccess, out result, out errorMessage);
        }

        byte programNumber = GetExtendedProgramNumber(devicePrefix);
        ushort baseWordOffset = GetExtendedWordBaseOffset(devicePrefix);
        int wordIndex = isBitAccess ? (hexNumber / BitsPerWord) : hexNumber;
        ushort wireAddress = (ushort)(baseWordOffset + wordIndex);

        result = new ToyopucAddress(original, devicePrefix, hexNumber, wireAddress, programNumber, isBitAccess, bitOffset);
        return true;
    }

    private static bool TryResolveExtendedFileRegister(
        string original,
        string devicePrefix,
        int hexNumber,
        bool isBitAccess,
        [NotNullWhen(true)] out ToyopucAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (isBitAccess)
        {
            errorMessage = $"확장 파일 레지스터(EB)는 비트 접근을 지원하지 않습니다: {original}";
            return false;
        }

        if (hexNumber > MaxExtendedFileRegisterAddress)
        {
            errorMessage = $"확장 파일 레지스터(EB) 주소 범위(0x00000~0x3FFFF)를 초과했습니다: {original}";
            return false;
        }

        ResolveExtendedFileRegisterBank(hexNumber, out byte programNumber, out ushort offsetInBank);
        result = new ToyopucAddress(original, devicePrefix, hexNumber, offsetInBank, programNumber, isBit: false, bitOffset: 0);
        return true;
    }

    private static void ResolveExtendedFileRegisterBank(int linearAddress, out byte programNumber, out ushort offsetInBank)
    {
        if (linearAddress < ExtendedBank1Limit)
        {
            programNumber = ExtendedProgramNumberBank0;
            offsetInBank = (ushort)linearAddress;
        }
        else if (linearAddress < ExtendedBank2Limit)
        {
            programNumber = ExtendedProgramNumberBank1;
            offsetInBank = (ushort)(linearAddress - ExtendedBank1Limit);
        }
        else
        {
            programNumber = ExtendedProgramNumberBank2;
            offsetInBank = (ushort)(linearAddress - ExtendedBank2Limit);
        }
    }

    private static byte GetExtendedProgramNumber(string devicePrefix) => devicePrefix switch
    {
        "H" or "EK" or "EV" or "ET" or "EC" or "EL" or "EX" or "EY" or "EM" or "ES" or "EN" => ExtendedProgramNumber0,
        "GX" or "GY" or "GM" => ExtendedProgramNumberGlobal,
        "U" => ExtendedProgramNumberBuffer,
        _ => 0xFF,
    };

    private static ushort GetExtendedWordBaseOffset(string devicePrefix) => devicePrefix switch
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
}
