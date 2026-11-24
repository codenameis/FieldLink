using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Delta;

/// <summary>Delta DVP 및 AS 시리즈의 디바이스 주소를 Modbus PDU 주소로 변환한 값 객체입니다. (사양서 §1)</summary>
public sealed class DeltaAddress
{
    private const int OctalRadix = 8;
    private const int SingleCharPrefixLength = 1;
    private const int TwoCharPrefixLength = 2;
    private const ushort DvpXBase = 0x0400;
    private const ushort DvpYBase = 0x0500;
    private const ushort DvpTBase = 0x0600;
    private const ushort DvpMLowBase = 0x0800;
    private const ushort DvpMHighBase = 0xB000;
    private const int DvpMSplitBoundary = 1536;
    private const int DvpMMaxIndex = 4095;
    private const ushort DvpCBase = 0x0E00;
    private const int DvpCMaxIndex = 255;
    private const ushort DvpDLowBase = 0x1000;
    private const ushort DvpDHighBase = 0x9000;
    private const int DvpDSplitBoundary = 4096;
    private const int DvpDMaxIndex = 11999;

    private const ushort AsSmBase = 0x4000;
    private const ushort AsSrBase = 0xC000;
    private const ushort AsHcBase = 0xFC00;
    private const ushort AsMBase = 0x0000;
    private const ushort AsSBase = 0x5000;
    private const ushort AsXBitBase = 0x6000;
    private const ushort AsXWordBase = 0x8000;
    private const ushort AsYBase = 0xA000;
    private const ushort AsDBase = 0x0000;
    private const ushort AsTBase = 0xE000;
    private const ushort AsCBase = 0xF000;
    private const ushort AsEBase = 0xFE00;
    private const int BitsPerWord = 16;
    private const int MaxBitIndexInWord = 15;
    private const char MaxOctalDigit = '7';
    private const char MinOctalDigit = '0';

    private DeltaAddress(
        string originalAddress,
        DeltaSeries series,
        string device,
        int index,
        int? bitIndex,
        ModbusArea area,
        ushort pduAddress)
    {
        OriginalAddress = originalAddress;
        Series = series;
        Device = device;
        Index = index;
        BitIndex = bitIndex;
        Area = area;
        PduAddress = pduAddress;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string OriginalAddress { get; }

    /// <summary>PLC 시리즈 구분입니다.</summary>
    public DeltaSeries Series { get; }

    /// <summary>디바이스 기호입니다. (예: "M", "D", "X", "Y", "SM", "SR", "HC" 등)</summary>
    public string Device { get; }

    /// <summary>디바이스 시작 인덱스 번호입니다.</summary>
    public int Index { get; }

    /// <summary>워드 내 비트 오프셋입니다. (지정된 경우 0~15)</summary>
    public int? BitIndex { get; }

    /// <summary>매핑되는 Modbus 메모리 영역입니다.</summary>
    public ModbusArea Area { get; }

    /// <summary>Modbus PDU 프레임에 실리는 16비트 주소입니다.</summary>
    public ushort PduAddress { get; }

    /// <summary>이 디바이스가 비트 접근 영역인지 여부입니다.</summary>
    public bool IsBit => Area is ModbusArea.Coil or ModbusArea.DiscreteInput;

    /// <summary>이 디바이스가 쓰기 가능한 영역인지 여부입니다.</summary>
    public bool IsWritable => Area is ModbusArea.Coil or ModbusArea.HoldingRegister;

    /// <summary>주소 문자열을 해석하여 <see cref="DeltaAddress"/> 인스턴스를 반환합니다.</summary>
    public static DeltaAddress Parse(string address, DeltaSeries series = DeltaSeries.Dvp)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!TryParse(address, series, out DeltaAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 해석을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        DeltaSeries series,
        [NotNullWhen(true)] out DeltaAddress? result)
    {
        return TryParse(address, series, out result, out _);
    }

    private static bool TryParse(
        string? address,
        DeltaSeries series,
        [NotNullWhen(true)] out DeltaAddress? result,
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
        return series == DeltaSeries.Dvp
            ? TryParseDvp(trimmed, out result, out errorMessage)
            : TryParseAs(trimmed, out result, out errorMessage);
    }

    private static bool TryParseDvp(
        string address,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (address.StartsWith("SM", StringComparison.Ordinal) ||
            address.StartsWith("SR", StringComparison.Ordinal) ||
            address.StartsWith("HC", StringComparison.Ordinal))
        {
            errorMessage = $"DVP 시리즈에서 지원하지 않는 디바이스입니다: {address}";
            return false;
        }

        char prefix = address[0];
        string numberText = address.Substring(SingleCharPrefixLength);

        if (prefix is 'X' or 'Y')
        {
            return TryParseDvpOctal(
                address,
                prefix == 'X' ? "X" : "Y",
                numberText,
                prefix == 'X' ? DvpXBase : DvpYBase,
                prefix == 'X' ? ModbusArea.DiscreteInput : ModbusArea.Coil,
                out result,
                out errorMessage);
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < 0)
        {
            errorMessage = $"유효하지 않은 DVP 주소 번호입니다: {address}";
            return false;
        }

        return prefix switch
        {
            'S' => TryBuildDvpFixedAddress(address, "S", number, 0, ModbusArea.Coil, out result, out errorMessage),
            'T' => TryBuildDvpFixedAddress(address, "T", number, DvpTBase, ModbusArea.HoldingRegister, out result, out errorMessage),
            'M' => TryBuildDvpM(address, number, out result, out errorMessage),
            'C' => TryBuildDvpC(address, number, out result, out errorMessage),
            'D' => TryBuildDvpD(address, number, out result, out errorMessage),
            _ => FailDvpPrefix(address, out errorMessage),
        };
    }

    private static bool TryParseDvpOctal(
        string original,
        string devicePrefix,
        string numberText,
        ushort baseOffset,
        ModbusArea area,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (numberText.Length == 0)
        {
            errorMessage = $"DVP {devicePrefix} 주소 번호가 비어 있습니다: {original}";
            return false;
        }

        int dec = 0;
        for (int i = 0; i < numberText.Length; i++)
        {
            char digit = numberText[i];
            if (digit < MinOctalDigit || digit > MaxOctalDigit)
            {
                errorMessage = $"DVP {devicePrefix} 주소는 8진수여야 합니다: {original}";
                return false;
            }

            if (dec > (ushort.MaxValue - baseOffset) / OctalRadix)
            {
                errorMessage = $"DVP {devicePrefix} 주소가 16비트 범위를 초과했습니다: {original}";
                return false;
            }

            dec = (dec * OctalRadix) + (digit - MinOctalDigit);
        }

        if (baseOffset + dec > ushort.MaxValue)
        {
            errorMessage = $"DVP {devicePrefix} 주소가 16비트 범위를 초과했습니다: {original}";
            return false;
        }

        ushort pdu = (ushort)(baseOffset + dec);
        result = new DeltaAddress(original, DeltaSeries.Dvp, devicePrefix, dec, null, area, pdu);
        return true;
    }

    private static bool TryBuildDvpM(
        string original,
        int number,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (number > DvpMMaxIndex)
        {
            errorMessage = $"DVP M 주소 범위(0~{DvpMMaxIndex})를 초과했습니다: {original}";
            return false;
        }

        ushort pdu = number < DvpMSplitBoundary
            ? (ushort)(number + DvpMLowBase)
            : (ushort)(number - DvpMSplitBoundary + DvpMHighBase);

        result = new DeltaAddress(original, DeltaSeries.Dvp, "M", number, null, ModbusArea.Coil, pdu);
        return true;
    }

    private static bool TryBuildDvpC(
        string original,
        int number,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (number > DvpCMaxIndex)
        {
            errorMessage = $"DVP C 주소 범위(0~{DvpCMaxIndex})를 초과했습니다: {original}";
            return false;
        }

        ushort pdu = (ushort)(number + DvpCBase);
        result = new DeltaAddress(original, DeltaSeries.Dvp, "C", number, null, ModbusArea.HoldingRegister, pdu);
        return true;
    }

    private static bool TryBuildDvpD(
        string original,
        int number,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (number > DvpDMaxIndex)
        {
            errorMessage = $"DVP D 주소 범위(0~{DvpDMaxIndex})를 초과했습니다: {original}";
            return false;
        }

        ushort pdu = number < DvpDSplitBoundary
            ? (ushort)(number + DvpDLowBase)
            : (ushort)(number - DvpDSplitBoundary + DvpDHighBase);

        result = new DeltaAddress(original, DeltaSeries.Dvp, "D", number, null, ModbusArea.HoldingRegister, pdu);
        return true;
    }

    private static bool TryBuildDvpFixedAddress(
        string original,
        string devicePrefix,
        int number,
        ushort baseOffset,
        ModbusArea area,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (baseOffset + (long)number > ushort.MaxValue)
        {
            errorMessage = $"DVP {devicePrefix} 주소가 16비트 범위를 초과했습니다: {original}";
            return false;
        }

        result = new DeltaAddress(original, DeltaSeries.Dvp, devicePrefix, number, null, area, (ushort)(baseOffset + number));
        return true;
    }

    private static bool FailDvpPrefix(string address, out string? errorMessage)
    {
        errorMessage = $"지원하지 않는 DVP 디바이스 접두사입니다: {address}";
        return false;
    }

    private static bool TryParseAs(
        string address,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (address.StartsWith("SM", StringComparison.Ordinal))
        {
            return TryParseAsSimple(address, "SM", TwoCharPrefixLength, AsSmBase, ModbusArea.Coil, out result, out errorMessage);
        }

        if (address.StartsWith("SR", StringComparison.Ordinal))
        {
            return TryParseAsSimple(address, "SR", TwoCharPrefixLength, AsSrBase, ModbusArea.HoldingRegister, out result, out errorMessage);
        }

        if (address.StartsWith("HC", StringComparison.Ordinal))
        {
            return TryParseAsSimple(address, "HC", TwoCharPrefixLength, AsHcBase, ModbusArea.HoldingRegister, out result, out errorMessage);
        }

        char prefix = address[0];
        string remainder = address.Substring(SingleCharPrefixLength);

        return prefix switch
        {
            'M' => TryParseAsSimple(address, "M", SingleCharPrefixLength, AsMBase, ModbusArea.Coil, out result, out errorMessage),
            'S' => TryParseAsSimple(address, "S", SingleCharPrefixLength, AsSBase, ModbusArea.Coil, out result, out errorMessage),
            'X' => TryParseAsIo(address, "X", remainder, AsXBitBase, AsXWordBase, isInput: true, out result, out errorMessage),
            'Y' => TryParseAsIo(address, "Y", remainder, AsYBase, AsYBase, isInput: false, out result, out errorMessage),
            'D' => TryParseAsSimple(address, "D", SingleCharPrefixLength, AsDBase, ModbusArea.HoldingRegister, out result, out errorMessage),
            'T' => TryParseAsSimple(address, "T", SingleCharPrefixLength, AsTBase, ModbusArea.HoldingRegister, out result, out errorMessage),
            'C' => TryParseAsSimple(address, "C", SingleCharPrefixLength, AsCBase, ModbusArea.HoldingRegister, out result, out errorMessage),
            'E' => TryParseAsSimple(address, "E", SingleCharPrefixLength, AsEBase, ModbusArea.HoldingRegister, out result, out errorMessage),
            _ => FailAsPrefix(address, out errorMessage),
        };
    }

    private static bool TryParseAsSimple(
        string original,
        string devicePrefix,
        int prefixLen,
        ushort baseOffset,
        ModbusArea area,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string numberText = original.Substring(prefixLen);
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber)
            || parsedNumber < 0
            || baseOffset + (long)parsedNumber > ushort.MaxValue)
        {
            errorMessage = $"유효하지 않은 AS {devicePrefix} 주소 번호입니다: {original}";
            return false;
        }

        ushort pdu = (ushort)(baseOffset + parsedNumber);
        result = new DeltaAddress(original, DeltaSeries.AS, devicePrefix, parsedNumber, null, area, pdu);
        return true;
    }

    private static bool TryParseAsIo(
        string original,
        string devicePrefix,
        string remainder,
        ushort bitBase,
        ushort wordBase,
        bool isInput,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        int dotIdx = remainder.IndexOf('.');
        if (dotIdx >= 0)
        {
            string wordStr = remainder.Substring(0, dotIdx);
            string bitStr = remainder.Substring(dotIdx + 1);

            if (!int.TryParse(wordStr, NumberStyles.None, CultureInfo.InvariantCulture, out int wordNum) || wordNum < 0 ||
                !int.TryParse(bitStr, NumberStyles.None, CultureInfo.InvariantCulture, out int bitNum) || bitNum is < 0 or > MaxBitIndexInWord ||
                bitBase + ((long)wordNum * BitsPerWord) + bitNum > ushort.MaxValue)
            {
                errorMessage = $"AS {devicePrefix} 비트 주소 형식이 올바르지 않습니다(0~{MaxBitIndexInWord}): {original}";
                return false;
            }

            ushort pdu = (ushort)(bitBase + (wordNum * BitsPerWord) + bitNum);
            ModbusArea area = isInput ? ModbusArea.DiscreteInput : ModbusArea.Coil;
            result = new DeltaAddress(original, DeltaSeries.AS, devicePrefix, wordNum, bitNum, area, pdu);
            return true;
        }

        if (!int.TryParse(remainder, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber)
            || parsedNumber < 0
            || wordBase + (long)parsedNumber > ushort.MaxValue)
        {
            errorMessage = $"유효하지 않은 AS {devicePrefix} 워드 주소 번호입니다: {original}";
            return false;
        }

        ushort wordPdu = (ushort)(wordBase + parsedNumber);
        ModbusArea wordArea = isInput ? ModbusArea.InputRegister : ModbusArea.HoldingRegister;
        result = new DeltaAddress(original, DeltaSeries.AS, devicePrefix, parsedNumber, null, wordArea, wordPdu);
        return true;
    }

    private static bool FailAsPrefix(string address, out string? errorMessage)
    {
        errorMessage = $"지원하지 않는 AS 디바이스 접두사입니다: {address}";
        return false;
    }
}
