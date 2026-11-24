using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Megmeet;

/// <summary>Megmeet PLC 디바이스 주소를 Modbus PDU 주소로 변환한 값 객체입니다. (사양서 §1)</summary>
public sealed class MegmeetAddress
{
    private const int OctalRadix = 8;
    private const int WordsPer32BitCounter = 2;
    private const int TwoCharPrefixLength = 2;
    private const int SingleCharPrefixLength = 1;
    private const char MinOctalDigit = '0';
    private const char MaxOctalDigit = '7';

    private const int IoMaxIndex = 255;
    private const int MBoundary = 2048;
    private const int MLowBase = 2000;
    private const int MHighBase = 12000;
    private const int MMaxIndex = 10239;
    private const int SmBoundary = 256;
    private const int SmLowBase = 4400;
    private const int SmHighBase = 30000;
    private const int SmMaxIndex = 511;
    private const int SBoundary = 1024;
    private const int SLowBase = 6000;
    private const int SHighBase = 31000;
    private const int SMaxIndex = 4095;
    private const int DMaxIndex = 7999;
    private const int SdBoundary = 256;
    private const int SdLowBase = 8000;
    private const int SdHighBase = 12000;
    private const int SdMaxIndex = 511;
    private const int ZBase = 8500;
    private const int ZMaxIndex = 15;
    private const int RBase = 13000;
    private const int RMaxIndex = 32767;
    private const int TBoundary = 256;
    private const int TBitLowBase = 8000;
    private const int TWordLowBase = 9000;
    private const int THighBase = 11000;
    private const int TMaxIndex = 511;
    private const int CBitBoundary = 256;
    private const int CBitLowBase = 9200;
    private const int CBitHighBase = 10000;
    private const int Counter32BitStartNumber = 200;
    private const int CWordDoubleBoundary = 256;
    private const int CWordLowBase = 9500;
    private const int CWordMidBase = 9700;
    private const int CWordHighBase = 10000;
    private const int CMaxIndex = 306;

    private static readonly string[] TwoCharPrefixes = ["SM", "SD"];

    private MegmeetAddress(string originalAddress, string device, int number, ModbusArea area, ushort pduAddress, bool isBit)
    {
        OriginalAddress = originalAddress;
        Device = device;
        Number = number;
        Area = area;
        PduAddress = pduAddress;
        IsBit = isBit;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string OriginalAddress { get; }

    /// <summary>디바이스 접두사입니다. (예: "M", "D", "X", "Y", "SM", "SD", "C")</summary>
    public string Device { get; }

    /// <summary>디바이스 인덱스 번호입니다. (X, Y는 8진수를 환산한 선형 인덱스입니다)</summary>
    public int Number { get; }

    /// <summary>매핑되는 Modbus 메모리 영역입니다.</summary>
    public ModbusArea Area { get; }

    /// <summary>16비트 Modbus PDU 주소입니다.</summary>
    public ushort PduAddress { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>32비트(2워드) 카운터 워드 영역(C200~C306) 여부입니다.</summary>
    public bool Is32BitWord => !IsBit && Device == "C" && Number >= Counter32BitStartNumber;

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static MegmeetAddress Parse(string address, bool isBitAccess = false)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!TryParse(address, isBitAccess, out MegmeetAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        bool isBitAccess,
        [NotNullWhen(true)] out MegmeetAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(address))
        {
            errorMessage = "주소 문자열이 비어 있습니다.";
            return false;
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (!TryExtractDevice(trimmed, out string devicePrefix, out string numberText))
        {
            errorMessage = $"지원하지 않는 Megmeet 디바이스 접두사입니다: {address}";
            return false;
        }

        return devicePrefix is "X" or "Y"
            ? TryParseOctal(trimmed, devicePrefix, numberText, out result, out errorMessage)
            : TryParseDecimal(trimmed, devicePrefix, numberText, isBitAccess, out result, out errorMessage);
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="MegmeetAddress"/>를 반환합니다.</summary>
    public MegmeetAddress Offset(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        long nextNumberLong = (long)Number + count;
        int maxDeviceIndex = GetMaxDeviceIndex(Device);
        if (nextNumberLong > maxDeviceIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, $"이동된 주소가 {Device} 최대 범위(0~{maxDeviceIndex})를 초과했습니다: {OriginalAddress}+{count}");
        }

        if (!IsBit && Device == "C" && Number < Counter32BitStartNumber && nextNumberLong >= Counter32BitStartNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "16비트 카운터(C0~C199)에서 32비트 카운터(C200~C306) 영역으로 오프셋 이동할 수 없습니다.");
        }

        int nextNumber = (int)nextNumberLong;
        if (!TryCalculatePdu(Device, nextNumber, IsBit, out ushort nextPdu))
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, $"이동된 주소가 16비트 PDU 범위를 초과했습니다: {OriginalAddress}+{count}");
        }

        return new MegmeetAddress($"{OriginalAddress}+{count}", Device, nextNumber, Area, nextPdu, IsBit);
    }

    internal void ValidateContiguousRange(long elementCount, int wordsPerElement, string paramName)
    {
        if (elementCount <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, elementCount, "요청 수량은 1 이상이어야 합니다.");
        }

        int numbersPerElement = IsBit
            ? 1
            : (Is32BitWord ? Math.Max(1, wordsPerElement / WordsPer32BitCounter) : wordsPerElement);
        long totalNumberSpan = elementCount * numbersPerElement;

        if (!IsBit &&
            Device == "C" &&
            Number < Counter32BitStartNumber &&
            Number + totalNumberSpan > Counter32BitStartNumber)
        {
            throw new ArgumentException(
                "16비트 카운터(C0~C199)와 32비트 카운터(C200~C306) 경계를 가로지르는 요청은 지원하지 않습니다.",
                paramName);
        }

        int maxDeviceIndex = GetMaxDeviceIndex(Device);
        if ((long)Number + totalNumberSpan - 1 > maxDeviceIndex)
        {
            throw new ArgumentOutOfRangeException(paramName, elementCount, $"요청 범위가 Megmeet {Device} 디바이스 상한({maxDeviceIndex})을 초과합니다.");
        }
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

        char firstChar = trimmed[0];
        devicePrefix = firstChar.ToString();
        numberText = trimmed.Substring(SingleCharPrefixLength);
        return numberText.Length > 0 && firstChar is 'X' or 'Y' or 'M' or 'S' or 'T' or 'C' or 'D' or 'Z' or 'R';
    }

    private static bool TryParseOctal(
        string original,
        string devicePrefix,
        string numberText,
        [NotNullWhen(true)] out MegmeetAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (numberText.Length == 0)
        {
            errorMessage = $"8진수 주소 파싱 실패: {original}";
            return false;
        }

        long accumulated = 0;
        for (int i = 0; i < numberText.Length; i++)
        {
            char digit = numberText[i];
            if (digit < MinOctalDigit || digit > MaxOctalDigit)
            {
                errorMessage = $"8진수 주소 파싱 실패: {original}";
                return false;
            }

            accumulated = (accumulated * OctalRadix) + (digit - MinOctalDigit);
            if (accumulated > IoMaxIndex)
            {
                errorMessage = $"8진수 주소가 범위(0~377)를 초과했습니다: {original}";
                return false;
            }
        }

        ushort octalValue = (ushort)accumulated;
        ModbusArea area = devicePrefix == "X" ? ModbusArea.DiscreteInput : ModbusArea.Coil;
        result = new MegmeetAddress(original, devicePrefix, octalValue, area, octalValue, isBit: true);
        return true;
    }

    private static bool TryParseDecimal(
        string original,
        string devicePrefix,
        string numberText,
        bool isBitAccess,
        [NotNullWhen(true)] out MegmeetAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber))
        {
            errorMessage = $"유효하지 않은 Megmeet 디바이스 번호: {original}";
            return false;
        }

        int maxDeviceIndex = GetMaxDeviceIndex(devicePrefix);
        if (parsedNumber > maxDeviceIndex)
        {
            errorMessage = $"Megmeet {devicePrefix} 주소 범위(0~{maxDeviceIndex})를 초과했습니다: {original}";
            return false;
        }

        bool isBit = devicePrefix switch
        {
            "M" or "SM" or "S" => true,
            "T" or "C" => isBitAccess,
            _ => false,
        };
        ModbusArea area = isBit ? ModbusArea.Coil : ModbusArea.HoldingRegister;
        if (!TryCalculatePdu(devicePrefix, parsedNumber, isBit, out ushort pdu))
        {
            errorMessage = $"Megmeet 주소가 16비트 PDU 범위를 초과했습니다: {original}";
            return false;
        }

        result = new MegmeetAddress(original, devicePrefix, parsedNumber, area, pdu, isBit);
        return true;
    }

    private static bool TryCalculatePdu(string devicePrefix, int number, bool isBit, out ushort pduAddress)
    {
        long raw = devicePrefix switch
        {
            "X" or "Y" => number,
            "M" => number < MBoundary ? (long)number + MLowBase : (number - MBoundary) + MHighBase,
            "SM" => number < SmBoundary ? (long)number + SmLowBase : (number - SmBoundary) + SmHighBase,
            "S" => number < SBoundary ? (long)number + SLowBase : (number - SBoundary) + SHighBase,
            "D" => number,
            "SD" => number < SdBoundary ? (long)number + SdLowBase : (number - SdBoundary) + SdHighBase,
            "Z" => (long)number + ZBase,
            "R" => (long)number + RBase,
            "T" => isBit
                ? (number < TBoundary ? (long)number + TBitLowBase : (number - TBoundary) + THighBase)
                : (number < TBoundary ? (long)number + TWordLowBase : (number - TBoundary) + THighBase),
            "C" => isBit
                ? (number < CBitBoundary ? (long)number + CBitLowBase : (number - CBitBoundary) + CBitHighBase)
                : CalculateCounterWordPdu(number),
            _ => -1,
        };

        if (raw < 0 || raw > ushort.MaxValue)
        {
            pduAddress = 0;
            return false;
        }

        pduAddress = (ushort)raw;
        return true;
    }

    private static long CalculateCounterWordPdu(int number)
    {
        if (number < Counter32BitStartNumber)
        {
            return (long)number + CWordLowBase;
        }

        if (number < CWordDoubleBoundary)
        {
            return (((long)number - Counter32BitStartNumber) * WordsPer32BitCounter) + CWordMidBase;
        }

        return (((long)number - CWordDoubleBoundary) * WordsPer32BitCounter) + CWordHighBase;
    }

    private static int GetMaxDeviceIndex(string device) => device switch
    {
        "X" or "Y" => IoMaxIndex,
        "M" => MMaxIndex,
        "SM" => SmMaxIndex,
        "S" => SMaxIndex,
        "D" => DMaxIndex,
        "SD" => SdMaxIndex,
        "Z" => ZMaxIndex,
        "R" => RMaxIndex,
        "T" => TMaxIndex,
        "C" => CMaxIndex,
        _ => 0,
    };
}
