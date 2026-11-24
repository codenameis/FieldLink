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
    private const int BitsPerWord = 16;
    private const int MaxBitIndexInWord = BitsPerWord - 1;
    private const char MinOctalDigit = '0';
    private const char MaxOctalDigit = '7';
    private const char BitSeparator = '.';

    private const ushort DvpXBase = 0x0400;
    private const ushort DvpYBase = 0x0500;
    private const int DvpIoMaxIndex = 255;
    private const int DvpSMaxIndex = 1023;
    private const ushort DvpTBase = 0x0600;
    private const int DvpTMaxIndex = 255;
    private const ushort DvpMLowBase = 0x0800;
    private const ushort DvpMHighBase = 0xB000;
    private const int DvpMSplitBoundary = 1536;
    private const int DvpMMaxIndex = 4095;
    private const ushort DvpCBase = 0x0E00;
    private const int DvpCounter32StartIndex = 200;
    private const int DvpCMaxIndex = 255;
    private const ushort DvpDLowBase = 0x1000;
    private const ushort DvpDHighBase = 0x9000;
    private const int DvpDSplitBoundary = 4096;
    private const int DvpDMaxIndex = 9999;

    private const ushort AsMBase = 0x0000;
    private const int AsMMaxIndex = 8191;
    private const ushort AsSmBase = 0x4000;
    private const int AsSmMaxIndex = 4095;
    private const ushort AsSBase = 0x5000;
    private const int AsSMaxIndex = 2047;
    private const ushort AsXBitBase = 0x6000;
    private const ushort AsXWordBase = 0x8000;
    private const ushort AsYBase = 0xA000;
    private const int AsIoMaxWordIndex = 63;
    private const ushort AsDBase = 0x0000;
    private const int AsDMaxIndex = 29999;
    private const ushort AsSrBase = 0xC000;
    private const int AsSrMaxIndex = 2047;
    private const ushort AsTBase = 0xE000;
    private const int AsTMaxIndex = 511;
    private const ushort AsCBase = 0xF000;
    private const int AsCMaxIndex = 511;
    private const ushort AsHcBase = 0xFC00;
    private const int AsHcMaxIndex = 255;
    private const ushort AsEBase = 0xFE00;
    private const int AsEMaxIndex = 9;

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
    public bool IsBit => Area is ModbusArea.Coil or ModbusArea.DiscreteInput || BitIndex.HasValue;

    /// <summary>이 디바이스가 쓰기 가능한 영역인지 여부입니다.</summary>
    public bool IsWritable => Area is ModbusArea.Coil or ModbusArea.HoldingRegister;

    internal bool IsWordBitAccess => Series == DeltaSeries.AS && Device == "D" && BitIndex.HasValue;

    /// <summary>주소 문자열을 해석하여 <see cref="DeltaAddress"/> 인스턴스를 반환합니다.</summary>
    public static DeltaAddress Parse(string address, DeltaSeries series = DeltaSeries.Dvp, bool isBitAccess = false)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!Enum.IsDefined(typeof(DeltaSeries), series))
        {
            throw new ArgumentOutOfRangeException(nameof(series), series, "지원하지 않는 Delta 시리즈입니다.");
        }

        if (!TryParse(address, series, isBitAccess, out DeltaAddress? result, out string? errorMessage))
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
        return TryParse(address, series, isBitAccess: false, out result, out _);
    }

    /// <summary>비트/워드 접근 모드를 지정하여 주소 문자열 해석을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        DeltaSeries series,
        bool isBitAccess,
        [NotNullWhen(true)] out DeltaAddress? result)
    {
        return TryParse(address, series, isBitAccess, out result, out _);
    }

    internal void ValidateContiguousRange(long elementCount, int wordsPerElement, string paramName)
    {
        if (elementCount <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, elementCount, "요청 수량은 1 이상이어야 합니다.");
        }

        if (IsWordBitAccess)
        {
            long endBit = ((long)Index * BitsPerWord) + BitIndex!.Value + elementCount - 1;
            if (endBit / BitsPerWord > AsDMaxIndex)
            {
                throw new ArgumentOutOfRangeException(paramName, elementCount, $"AS D 비트 주소가 최대 범위(D0.0~D{AsDMaxIndex}.{MaxBitIndexInWord})를 초과합니다.");
            }

            return;
        }

        if (Series == DeltaSeries.AS && Device is "X" or "Y" && IsBit)
        {
            long endBit = ((long)Index * BitsPerWord) + BitIndex!.Value + elementCount - 1;
            long maxIoBit = ((long)AsIoMaxWordIndex * BitsPerWord) + MaxBitIndexInWord;
            if (endBit > maxIoBit)
            {
                throw new ArgumentOutOfRangeException(paramName, elementCount, $"AS {Device} 비트 주소가 최대 범위(0~{AsIoMaxWordIndex}.{MaxBitIndexInWord})를 초과합니다.");
            }

            return;
        }

        if (Series == DeltaSeries.Dvp && !IsBit && Device == "C")
        {
            if (Index < DvpCounter32StartIndex && (long)Index + elementCount > DvpCounter32StartIndex)
            {
                throw new ArgumentException("DVP 16비트 카운터(C0~C199)와 32비트 카운터(C200~C255) 경계를 넘는 요청입니다.", paramName);
            }
        }

        long spanUnits = IsBit ? elementCount : elementCount * wordsPerElement;
        int maxDeviceIndex = GetMaxDeviceIndex(Series, Device);
        if ((long)Index + spanUnits - 1 > maxDeviceIndex)
        {
            throw new ArgumentOutOfRangeException(paramName, elementCount, $"요청 범위가 {Series} {Device} 디바이스 상한({maxDeviceIndex})을 초과합니다.");
        }
    }

    private static bool TryParse(
        string? address,
        DeltaSeries series,
        bool isBitAccess,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (!Enum.IsDefined(typeof(DeltaSeries), series))
        {
            errorMessage = $"지원하지 않는 Delta 시리즈입니다: {series}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            errorMessage = "주소 문자열이 비어 있습니다.";
            return false;
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        return series == DeltaSeries.Dvp
            ? TryParseDvp(trimmed, isBitAccess, out result, out errorMessage)
            : TryParseAs(trimmed, isBitAccess, out result, out errorMessage);
    }

    private static bool TryParseDvp(
        string address,
        bool isBitAccess,
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
            ushort ioBase = prefix == 'X' ? DvpXBase : DvpYBase;
            ModbusArea ioArea = prefix == 'X' ? ModbusArea.DiscreteInput : ModbusArea.Coil;
            return TryParseDvpOctal(address, prefix.ToString(), numberText, ioBase, ioArea, out result, out errorMessage);
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
        {
            errorMessage = $"유효하지 않은 DVP 주소 번호입니다: {address}";
            return false;
        }

        ModbusArea timerCounterArea = isBitAccess ? ModbusArea.Coil : ModbusArea.HoldingRegister;

        return prefix switch
        {
            'S' when number <= DvpSMaxIndex => TryBuildDvpAddress(address, "S", number, ModbusArea.Coil, number, out result, out errorMessage),
            'T' when number <= DvpTMaxIndex => TryBuildDvpAddress(address, "T", number, timerCounterArea, number + DvpTBase, out result, out errorMessage),
            'M' => TryBuildDvpM(address, number, out result, out errorMessage),
            'C' => TryBuildDvpC(address, number, timerCounterArea, out result, out errorMessage),
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

        long accumulated = 0;
        for (int i = 0; i < numberText.Length; i++)
        {
            char digit = numberText[i];
            if (digit < MinOctalDigit || digit > MaxOctalDigit)
            {
                errorMessage = $"DVP {devicePrefix} 주소는 8진수여야 합니다: {original}";
                return false;
            }

            accumulated = (accumulated * OctalRadix) + (digit - MinOctalDigit);
            if (accumulated > DvpIoMaxIndex)
            {
                errorMessage = $"DVP {devicePrefix} 주소 범위(X0~X377)를 초과했습니다: {original}";
                return false;
            }
        }

        int decimalValue = (int)accumulated;
        ushort pdu = (ushort)(baseOffset + decimalValue);
        result = new DeltaAddress(original, DeltaSeries.Dvp, devicePrefix, decimalValue, null, area, pdu);
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
        ModbusArea area,
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
        result = new DeltaAddress(original, DeltaSeries.Dvp, "C", number, null, area, pdu);
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

    private static bool TryBuildDvpAddress(
        string original,
        string devicePrefix,
        int number,
        ModbusArea area,
        long pduValue,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (pduValue < 0 || pduValue > ushort.MaxValue)
        {
            errorMessage = $"DVP {devicePrefix} 주소가 16비트 PDU 공간을 초과했습니다: {original}";
            return false;
        }

        result = new DeltaAddress(original, DeltaSeries.Dvp, devicePrefix, number, null, area, (ushort)pduValue);
        return true;
    }

    private static bool FailDvpPrefix(string address, out string? errorMessage)
    {
        errorMessage = $"지원하지 않거나 범위를 벗어난 DVP 디바이스입니다: {address}";
        return false;
    }

    private static bool TryParseAs(
        string address,
        bool isBitAccess,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        ModbusArea timerCounterArea = isBitAccess ? ModbusArea.Coil : ModbusArea.HoldingRegister;

        if (address.StartsWith("SM", StringComparison.Ordinal))
        {
            return TryParseAsSimple(address, "SM", TwoCharPrefixLength, AsSmBase, AsSmMaxIndex, ModbusArea.Coil, out result, out errorMessage);
        }

        if (address.StartsWith("SR", StringComparison.Ordinal))
        {
            return TryParseAsSimple(address, "SR", TwoCharPrefixLength, AsSrBase, AsSrMaxIndex, ModbusArea.HoldingRegister, out result, out errorMessage);
        }

        if (address.StartsWith("HC", StringComparison.Ordinal))
        {
            return TryParseAsSimple(address, "HC", TwoCharPrefixLength, AsHcBase, AsHcMaxIndex, timerCounterArea, out result, out errorMessage);
        }

        char prefix = address[0];
        string remainder = address.Substring(SingleCharPrefixLength);

        return prefix switch
        {
            'M' => TryParseAsSimple(address, "M", SingleCharPrefixLength, AsMBase, AsMMaxIndex, ModbusArea.Coil, out result, out errorMessage),
            'S' => TryParseAsSimple(address, "S", SingleCharPrefixLength, AsSBase, AsSMaxIndex, ModbusArea.Coil, out result, out errorMessage),
            'X' => TryParseAsIo(address, "X", remainder, AsXBitBase, AsXWordBase, isInput: true, isBitAccess, out result, out errorMessage),
            'Y' => TryParseAsIo(address, "Y", remainder, AsYBase, AsYBase, isInput: false, isBitAccess, out result, out errorMessage),
            'D' => TryParseAsDataRegister(address, remainder, out result, out errorMessage),
            'T' => TryParseAsSimple(address, "T", SingleCharPrefixLength, AsTBase, AsTMaxIndex, timerCounterArea, out result, out errorMessage),
            'C' => TryParseAsSimple(address, "C", SingleCharPrefixLength, AsCBase, AsCMaxIndex, timerCounterArea, out result, out errorMessage),
            'E' => TryParseAsSimple(address, "E", SingleCharPrefixLength, AsEBase, AsEMaxIndex, ModbusArea.HoldingRegister, out result, out errorMessage),
            _ => FailAsPrefix(address, out result, out errorMessage),
        };
    }

    private static bool TryParseAsDataRegister(
        string original,
        string remainder,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        int dotIndex = remainder.IndexOf(BitSeparator);
        if (dotIndex < 0)
        {
            return TryParseAsSimple(original, "D", SingleCharPrefixLength, AsDBase, AsDMaxIndex, ModbusArea.HoldingRegister, out result, out errorMessage);
        }

        string wordText = remainder.Substring(0, dotIndex);
        string bitText = remainder.Substring(dotIndex + 1);
        if (!int.TryParse(wordText, NumberStyles.None, CultureInfo.InvariantCulture, out int wordNumber) ||
            wordNumber > AsDMaxIndex ||
            !int.TryParse(bitText, NumberStyles.None, CultureInfo.InvariantCulture, out int bitNumber) ||
            bitNumber > MaxBitIndexInWord)
        {
            errorMessage = $"AS D 비트 주소 형식이 올바르지 않습니다(D0.0~D{AsDMaxIndex}.{MaxBitIndexInWord}): {original}";
            return false;
        }

        result = new DeltaAddress(original, DeltaSeries.AS, "D", wordNumber, bitNumber, ModbusArea.HoldingRegister, (ushort)wordNumber);
        return true;
    }

    private static bool TryParseAsSimple(
        string original,
        string devicePrefix,
        int prefixLength,
        ushort baseOffset,
        int maxIndex,
        ModbusArea area,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        string numberText = original.Substring(prefixLength);
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber)
            || parsedNumber > maxIndex)
        {
            errorMessage = $"유효하지 않은 AS {devicePrefix} 주소 번호(0~{maxIndex})입니다: {original}";
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
        bool isBitAccess,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        int dotIndex = remainder.IndexOf(BitSeparator);
        if (dotIndex >= 0)
        {
            string wordText = remainder.Substring(0, dotIndex);
            string bitText = remainder.Substring(dotIndex + 1);

            if (!int.TryParse(wordText, NumberStyles.None, CultureInfo.InvariantCulture, out int wordNumber) || wordNumber > AsIoMaxWordIndex ||
                !int.TryParse(bitText, NumberStyles.None, CultureInfo.InvariantCulture, out int bitNumber) || bitNumber > MaxBitIndexInWord)
            {
                errorMessage = $"AS {devicePrefix} 비트 주소 형식이 올바르지 않습니다(0.0~{AsIoMaxWordIndex}.{MaxBitIndexInWord}): {original}";
                return false;
            }

            ushort pdu = (ushort)(bitBase + (wordNumber * BitsPerWord) + bitNumber);
            ModbusArea area = isInput ? ModbusArea.DiscreteInput : ModbusArea.Coil;
            result = new DeltaAddress(original, DeltaSeries.AS, devicePrefix, wordNumber, bitNumber, area, pdu);
            return true;
        }

        if (!int.TryParse(remainder, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber)
            || parsedNumber > AsIoMaxWordIndex)
        {
            errorMessage = $"유효하지 않은 AS {devicePrefix} 주소 번호(0~{AsIoMaxWordIndex})입니다: {original}";
            return false;
        }

        if (isBitAccess)
        {
            ushort bitPdu = (ushort)(bitBase + (parsedNumber * BitsPerWord));
            ModbusArea bitArea = isInput ? ModbusArea.DiscreteInput : ModbusArea.Coil;
            result = new DeltaAddress(original, DeltaSeries.AS, devicePrefix, parsedNumber, bitIndex: 0, bitArea, bitPdu);
            return true;
        }

        ushort wordPdu = (ushort)(wordBase + parsedNumber);
        ModbusArea wordArea = isInput ? ModbusArea.InputRegister : ModbusArea.HoldingRegister;
        result = new DeltaAddress(original, DeltaSeries.AS, devicePrefix, parsedNumber, null, wordArea, wordPdu);
        return true;
    }

    private static int GetMaxDeviceIndex(DeltaSeries series, string device) => series switch
    {
        DeltaSeries.Dvp => device switch
        {
            "S" => DvpSMaxIndex,
            "X" or "Y" => DvpIoMaxIndex,
            "T" => DvpTMaxIndex,
            "M" => DvpMMaxIndex,
            "C" => DvpCMaxIndex,
            "D" => DvpDMaxIndex,
            _ => 0,
        },
        DeltaSeries.AS => device switch
        {
            "M" => AsMMaxIndex,
            "SM" => AsSmMaxIndex,
            "S" => AsSMaxIndex,
            "X" or "Y" => AsIoMaxWordIndex,
            "D" => AsDMaxIndex,
            "SR" => AsSrMaxIndex,
            "T" => AsTMaxIndex,
            "C" => AsCMaxIndex,
            "HC" => AsHcMaxIndex,
            "E" => AsEMaxIndex,
            _ => 0,
        },
        _ => 0,
    };

    private static bool FailAsPrefix(
        string address,
        [NotNullWhen(true)] out DeltaAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = $"지원하지 않는 AS 디바이스 접두사입니다: {address}";
        return false;
    }
}
