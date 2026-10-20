using System;
using System.Diagnostics.CodeAnalysis;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Delta;

/// <summary>Delta DVP 및 AS 시리즈의 디바이스 주소를 Modbus PDU 주소로 변환한 값 객체입니다. (사양서 §1)</summary>
public sealed class DeltaAddress
{
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
        string numberText = address.Substring(1);

        if (!int.TryParse(numberText, out int number) || number < 0)
        {
            errorMessage = $"유효하지 않은 DVP 주소 번호입니다: {address}";
            return false;
        }

        return prefix switch
        {
            'S' => BuildDvpAddress(address, "S", number, ModbusArea.Coil, (ushort)number, out result),
            'X' => TryParseDvpOctal(address, "X", numberText, 0x0400, ModbusArea.DiscreteInput, out result, out errorMessage),
            'Y' => TryParseDvpOctal(address, "Y", numberText, 0x0500, ModbusArea.Coil, out result, out errorMessage),
            'T' => BuildDvpAddress(address, "T", number, ModbusArea.HoldingRegister, (ushort)(number + 0x0600), out result),
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

        for (int i = 0; i < numberText.Length; i++)
        {
            if (numberText[i] is < '0' or > '7')
            {
                errorMessage = $"DVP {devicePrefix} 주소는 8진수여야 합니다: {original}";
                return false;
            }
        }

        int dec = Convert.ToInt32(numberText, 8);
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

        if (number > 4095)
        {
            errorMessage = $"DVP M 주소 범위(0~4095)를 초과했습니다: {original}";
            return false;
        }

        ushort pdu = number < 1536
            ? (ushort)(number + 0x0800)
            : (ushort)(number - 1536 + 0xB000);

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

        if (number > 255)
        {
            errorMessage = $"DVP C 주소 범위(0~255)를 초과했습니다: {original}";
            return false;
        }

        ushort pdu = (ushort)(number + 0x0E00);
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

        if (number > 11999)
        {
            errorMessage = $"DVP D 주소 범위(0~11999)를 초과했습니다: {original}";
            return false;
        }

        ushort pdu = number < 4096
            ? (ushort)(number + 0x1000)
            : (ushort)(number - 4096 + 0x9000);

        result = new DeltaAddress(original, DeltaSeries.Dvp, "D", number, null, ModbusArea.HoldingRegister, pdu);
        return true;
    }

    private static bool BuildDvpAddress(
        string original,
        string devicePrefix,
        int number,
        ModbusArea area,
        ushort pdu,
        [NotNullWhen(true)] out DeltaAddress? result)
    {
        result = new DeltaAddress(original, DeltaSeries.Dvp, devicePrefix, number, null, area, pdu);
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
            return TryParseAsSimple(address, "SM", 2, 0x4000, ModbusArea.Coil, out result, out errorMessage);
        }

        if (address.StartsWith("SR", StringComparison.Ordinal))
        {
            return TryParseAsSimple(address, "SR", 2, 0xC000, ModbusArea.HoldingRegister, out result, out errorMessage);
        }

        if (address.StartsWith("HC", StringComparison.Ordinal))
        {
            return TryParseAsSimple(address, "HC", 2, 0xFC00, ModbusArea.HoldingRegister, out result, out errorMessage);
        }

        char prefix = address[0];
        string remainder = address.Substring(1);

        return prefix switch
        {
            'M' => TryParseAsSimple(address, "M", 1, 0, ModbusArea.Coil, out result, out errorMessage),
            'S' => TryParseAsSimple(address, "S", 1, 0x5000, ModbusArea.Coil, out result, out errorMessage),
            'X' => TryParseAsIo(address, "X", remainder, 0x6000, 0x8000, isInput: true, out result, out errorMessage),
            'Y' => TryParseAsIo(address, "Y", remainder, 0xA000, 0xA000, isInput: false, out result, out errorMessage),
            'D' => TryParseAsSimple(address, "D", 1, 0, ModbusArea.HoldingRegister, out result, out errorMessage),
            'T' => TryParseAsSimple(address, "T", 1, 0xE000, ModbusArea.HoldingRegister, out result, out errorMessage),
            'C' => TryParseAsSimple(address, "C", 1, 0xF000, ModbusArea.HoldingRegister, out result, out errorMessage),
            'E' => TryParseAsSimple(address, "E", 1, 0xFE00, ModbusArea.HoldingRegister, out result, out errorMessage),
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
        if (!int.TryParse(numberText, out int parsedNumber) || parsedNumber < 0)
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

            if (!int.TryParse(wordStr, out int wordNum) || wordNum < 0 ||
                !int.TryParse(bitStr, out int bitNum) || bitNum is < 0 or > 15)
            {
                errorMessage = $"AS {devicePrefix} 비트 주소 형식이 올바르지 않습니다(0~15): {original}";
                return false;
            }

            ushort pdu = (ushort)(bitBase + (wordNum * 16) + bitNum);
            ModbusArea area = isInput ? ModbusArea.DiscreteInput : ModbusArea.Coil;
            result = new DeltaAddress(original, DeltaSeries.AS, devicePrefix, wordNum, bitNum, area, pdu);
            return true;
        }

        if (!int.TryParse(remainder, out int parsedNumber) || parsedNumber < 0)
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
