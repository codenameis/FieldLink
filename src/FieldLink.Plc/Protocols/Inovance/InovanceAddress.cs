using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Inovance;

/// <summary>Inovance PLC 디바이스 주소를 Modbus PDU 주소로 변환한 값 객체입니다. (사양서 §1)</summary>
public sealed class InovanceAddress
{
    private const int OctalRadix = 8;
    private const int BitsPerByte = 8;
    private const int BytesPerWord = 2;
    private const int BitsPerWord = 16;
    private const int WordsPer32BitCounter = 2;
    private const int MaxBitIndexInByte = BitsPerByte - 1;
    private const int SingleCharPrefixLength = 1;
    private const int TwoCharPrefixLength = 2;
    private const int ThreeCharPrefixLength = 3;
    private const char MinOctalDigit = '0';
    private const char MaxOctalDigit = '7';
    private const char BitSeparator = '.';

    private const int AmIoBitMaxByteIndex = 8191;
    private const int AmMdMaxIndex = 32767;
    private const int AmMwMaxIndex = 65535;
    private const int AmMbMaxIndex = (AmMwMaxIndex * BytesPerWord) + 1;
    private const int AmMxMaxBitNumber = (AmMbMaxIndex * BitsPerByte) + MaxBitIndexInByte;

    private const ushort H3uXBasePdu = 0xF800;
    private const ushort H3uYBasePdu = 0xFC00;
    private const int H3uIoMaxOctal = 255;
    private const ushort H3uSmSdBasePdu = 0x2400;
    private const int H3uSmSdMaxIndex = 1023;
    private const int H3uMHighBoundary = 8000;
    private const ushort H3uMHighBasePdu = 0x1F40;
    private const int H3uMMaxIndex = 8511;
    private const ushort H3uSBasePdu = 0xE000;
    private const int H3uSMaxIndex = 4095;
    private const int H3uDMaxIndex = 8511;
    private const ushort H3uRBasePdu = 0x3000;
    private const int H3uRMaxIndex = 32767;
    private const ushort H3uTBasePdu = 0xF000;
    private const int H3uTMaxIndex = 511;
    private const ushort H3uCBasePdu = 0xF400;
    private const int H3uCounter32Start = 200;
    private const int H3uCMaxIndex = 255;
    private const ushort H3uCDoubleWordBasePdu = 0xF700;

    private const int H5uIoMaxOctal = 1023;
    private const int H5uMMaxIndex = 7999;
    private const ushort H5uBBasePdu = 0x3000;
    private const int H5uBMaxIndex = 32767;
    private const ushort H5uSBasePdu = 0xE000;
    private const int H5uSMaxIndex = 4095;
    private const int H5uDMaxIndex = 7999;
    private const ushort H5uRBasePdu = 0x3000;
    private const int H5uRMaxIndex = 32767;

    private InovanceAddress(
        string originalAddress,
        InovanceSeries series,
        ModbusArea area,
        ushort pduAddress,
        bool isBit,
        byte? bitIndexInWord,
        string deviceType,
        int number,
        bool usesExtendedFunctionCode)
    {
        OriginalAddress = originalAddress;
        Series = series;
        Area = area;
        PduAddress = pduAddress;
        IsBit = isBit;
        BitIndexInWord = bitIndexInWord;
        DeviceType = deviceType;
        Number = number;
        UsesExtendedFunctionCode = usesExtendedFunctionCode;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string OriginalAddress { get; }

    /// <summary>PLC 시리즈 구분입니다.</summary>
    public InovanceSeries Series { get; }

    /// <summary>매핑된 Modbus 메모리 영역입니다.</summary>
    public ModbusArea Area { get; }

    /// <summary>16비트 Modbus PDU 주소입니다.</summary>
    public ushort PduAddress { get; }

    /// <summary>비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>워드 내 비트 인덱스 (AM %MX 접근 시 0~15)입니다.</summary>
    public byte? BitIndexInWord { get; }

    /// <summary>디바이스 종류 문자열입니다. (예: "D", "M", "C", "MB", "SMX", "SDW")</summary>
    public string DeviceType { get; }

    /// <summary>파싱된 디바이스 번호입니다.</summary>
    public int Number { get; }

    /// <summary>확장 기능 코드(표준 FC + 0x30: AM SMX/SM, SDW/SD) 사용 여부입니다.</summary>
    public bool UsesExtendedFunctionCode { get; }

    /// <summary>32비트 워드 디바이스(AM MD 또는 H3U C200~C255) 여부입니다.</summary>
    public bool Is32BitWord =>
        !IsBit &&
        ((Series == InovanceSeries.AM && DeviceType == "MD") ||
         (Series == InovanceSeries.H3u && DeviceType == "C" && Number >= H3uCounter32Start));

    internal bool IsByteAddress => Series == InovanceSeries.AM && DeviceType == "MB";

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static InovanceAddress Parse(string address, InovanceSeries series, bool isBitAccess = false)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!Enum.IsDefined(typeof(InovanceSeries), series))
        {
            throw new ArgumentOutOfRangeException(nameof(series), series, "지원하지 않는 시리즈입니다.");
        }

        if (!TryParse(address, series, isBitAccess, out InovanceAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        InovanceSeries series,
        bool isBitAccess,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (!Enum.IsDefined(typeof(InovanceSeries), series))
        {
            errorMessage = $"지원하지 않는 Inovance 시리즈입니다: {series}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            errorMessage = "주소 문자열이 비어 있습니다.";
            return false;
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (trimmed.StartsWith("%", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(SingleCharPrefixLength);
        }

        if (trimmed.Length == 0)
        {
            errorMessage = $"유효하지 않은 Inovance 주소입니다: {address}";
            return false;
        }

        return series switch
        {
            InovanceSeries.AM => TryParseAm(trimmed, isBitAccess, out result, out errorMessage),
            InovanceSeries.H3u => TryParseH3u(trimmed, isBitAccess, out result, out errorMessage),
            InovanceSeries.H5u => TryParseH5u(trimmed, out result, out errorMessage),
            _ => throw new ArgumentOutOfRangeException(nameof(series), series, "지원하지 않는 시리즈입니다."),
        };
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="InovanceAddress"/>를 반환합니다.</summary>
    public InovanceAddress Offset(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        if (count == 0)
        {
            return this;
        }

        ValidateContiguousRange((long)count + 1, nameof(count));

        if (IsByteAddress)
        {
            int nextByteNumber = Number + count;
            ushort nextByteWordPdu = (ushort)(nextByteNumber / BytesPerWord);
            return new InovanceAddress($"{OriginalAddress}+{count}", Series, Area, nextByteWordPdu, isBit: false, bitIndexInWord: null, DeviceType, nextByteNumber, UsesExtendedFunctionCode);
        }

        if (BitIndexInWord.HasValue)
        {
            int totalBitIndex = (PduAddress * BitsPerWord) + BitIndexInWord.Value + count;
            ushort nextWordPdu = (ushort)(totalBitIndex / BitsPerWord);
            byte nextBitInWord = (byte)(totalBitIndex % BitsPerWord);
            return new InovanceAddress($"{OriginalAddress}+{count}", Series, Area, nextWordPdu, IsBit, nextBitInWord, DeviceType, Number + count, UsesExtendedFunctionCode);
        }

        int pduStep = Is32BitWord ? count * WordsPer32BitCounter : count;
        ushort nextPdu = (ushort)(PduAddress + pduStep);
        return new InovanceAddress($"{OriginalAddress}+{count}", Series, Area, nextPdu, IsBit, bitIndexInWord: null, DeviceType, Number + count, UsesExtendedFunctionCode);
    }

    internal InovanceAddress ToByteWordAddress(int wordIndex)
    {
        if (wordIndex < 0 || wordIndex > AmMwMaxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(wordIndex), wordIndex, $"AM 워드 주소 범위(0~{AmMwMaxIndex})를 초과했습니다.");
        }

        return new InovanceAddress(
            OriginalAddress,
            Series,
            ModbusArea.HoldingRegister,
            (ushort)wordIndex,
            isBit: false,
            bitIndexInWord: null,
            "MW",
            wordIndex,
            usesExtendedFunctionCode: false);
    }

    internal void ValidateContiguousRange(long unitCount, string paramName)
    {
        if (unitCount <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, unitCount, "요청 수량은 1 이상이어야 합니다.");
        }

        if (IsByteAddress)
        {
            if ((long)Number + unitCount - 1 > AmMbMaxIndex)
            {
                throw new ArgumentOutOfRangeException(paramName, unitCount, $"AM MB 바이트 주소가 최대 범위(0~{AmMbMaxIndex})를 초과합니다.");
            }

            return;
        }

        if (BitIndexInWord.HasValue)
        {
            if ((long)Number + unitCount - 1 > AmMxMaxBitNumber)
            {
                throw new ArgumentOutOfRangeException(paramName, unitCount, $"AM MX 비트 주소가 최대 워드 범위(0~{AmMwMaxIndex})를 초과합니다.");
            }

            return;
        }

        if (Series == InovanceSeries.H3u && !IsBit && DeviceType == "C")
        {
            if (Number < H3uCounter32Start && (long)Number + unitCount > H3uCounter32Start)
            {
                throw new ArgumentException("H3U 16비트 카운터(C0~C199)와 32비트 카운터(C200~C255) 경계를 넘는 요청입니다.", paramName);
            }
        }

        int maxDeviceIndex = GetMaxDeviceIndex(Series, DeviceType, IsBit);
        if ((long)Number + unitCount - 1 > maxDeviceIndex)
        {
            throw new ArgumentOutOfRangeException(paramName, unitCount, $"요청 범위가 {Series} {DeviceType} 디바이스 상한({maxDeviceIndex})을 초과합니다.");
        }

        long pduUnits = Is32BitWord ? unitCount * WordsPer32BitCounter : unitCount;
        if ((long)PduAddress + pduUnits - 1 > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(paramName, unitCount, $"요청 범위가 Modbus 16비트 주소 공간(0~{ushort.MaxValue})을 초과합니다.");
        }
    }

    private static bool TryParseAm(
        string trimmed,
        bool isBitAccess,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        if (trimmed.StartsWith("SMX", StringComparison.Ordinal))
        {
            return ParseAmBitArea(trimmed, ThreeCharPrefixLength, "SMX", ModbusArea.Coil, usesExtendedFunctionCode: true, out result, out errorMessage);
        }

        if (trimmed.StartsWith("SDW", StringComparison.Ordinal))
        {
            return ParseAmWordNumber(trimmed, ThreeCharPrefixLength, "SDW", AmMwMaxIndex, static number => (ushort)number, usesExtendedFunctionCode: true, out result, out errorMessage);
        }

        if (trimmed.StartsWith("QX", StringComparison.Ordinal) || trimmed.StartsWith("Q", StringComparison.Ordinal))
        {
            int prefixLength = trimmed.StartsWith("QX", StringComparison.Ordinal) ? TwoCharPrefixLength : SingleCharPrefixLength;
            return ParseAmBitArea(trimmed, prefixLength, "QX", ModbusArea.Coil, usesExtendedFunctionCode: false, out result, out errorMessage);
        }

        if (trimmed.StartsWith("IX", StringComparison.Ordinal) || trimmed.StartsWith("I", StringComparison.Ordinal))
        {
            int prefixLength = trimmed.StartsWith("IX", StringComparison.Ordinal) ? TwoCharPrefixLength : SingleCharPrefixLength;
            return ParseAmBitArea(trimmed, prefixLength, "IX", ModbusArea.DiscreteInput, usesExtendedFunctionCode: false, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MX", StringComparison.Ordinal))
        {
            return ParseAmMx(trimmed, isBitAccess, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MB", StringComparison.Ordinal))
        {
            return ParseAmMb(trimmed, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MD", StringComparison.Ordinal))
        {
            return ParseAmWordNumber(trimmed, TwoCharPrefixLength, "MD", AmMdMaxIndex, static number => (ushort)(number * WordsPer32BitCounter), usesExtendedFunctionCode: false, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MW", StringComparison.Ordinal))
        {
            return ParseAmWordNumber(trimmed, TwoCharPrefixLength, "MW", AmMwMaxIndex, static number => (ushort)number, usesExtendedFunctionCode: false, out result, out errorMessage);
        }

        if (trimmed.StartsWith("SM", StringComparison.Ordinal))
        {
            return ParseAmBitArea(trimmed, TwoCharPrefixLength, "SMX", ModbusArea.Coil, usesExtendedFunctionCode: true, out result, out errorMessage);
        }

        if (trimmed.StartsWith("SD", StringComparison.Ordinal))
        {
            return ParseAmWordNumber(trimmed, TwoCharPrefixLength, "SDW", AmMwMaxIndex, static number => (ushort)number, usesExtendedFunctionCode: true, out result, out errorMessage);
        }

        if (trimmed.StartsWith("M", StringComparison.Ordinal))
        {
            return ParseAmWordNumber(trimmed, SingleCharPrefixLength, "MW", AmMwMaxIndex, static number => (ushort)number, usesExtendedFunctionCode: false, out result, out errorMessage);
        }

        result = null;
        errorMessage = $"지원하지 않는 Inovance AM 주소입니다: {trimmed}";
        return false;
    }

    private static bool ParseAmBitArea(
        string original,
        int prefixLength,
        string deviceType,
        ModbusArea area,
        bool usesExtendedFunctionCode,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        string numberText = original.Substring(prefixLength);
        int dotIndex = numberText.IndexOf(BitSeparator);
        if (dotIndex >= 0)
        {
            if (!int.TryParse(numberText.Substring(0, dotIndex), NumberStyles.None, CultureInfo.InvariantCulture, out int byteIndex) ||
                byteIndex > AmIoBitMaxByteIndex ||
                !byte.TryParse(numberText.Substring(dotIndex + 1), NumberStyles.None, CultureInfo.InvariantCulture, out byte bitOffset) ||
                bitOffset > MaxBitIndexInByte)
            {
                errorMessage = $"유효하지 않은 AM 비트 주소: {original}";
                return false;
            }

            ushort bitPdu = (ushort)((byteIndex * BitsPerByte) + bitOffset);
            result = new InovanceAddress(original, InovanceSeries.AM, area, bitPdu, isBit: true, bitIndexInWord: null, deviceType, bitPdu, usesExtendedFunctionCode);
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int singleNumber) ||
            singleNumber > AmMwMaxIndex)
        {
            errorMessage = $"유효하지 않은 AM 주소 번호: {original}";
            return false;
        }

        result = new InovanceAddress(original, InovanceSeries.AM, area, (ushort)singleNumber, isBit: true, bitIndexInWord: null, deviceType, singleNumber, usesExtendedFunctionCode);
        return true;
    }

    private static bool ParseAmMx(
        string original,
        bool isBitAccess,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        string numberText = original.Substring(TwoCharPrefixLength);
        int dotIndex = numberText.IndexOf(BitSeparator);
        if (dotIndex >= 0 || isBitAccess)
        {
            string bytePart = dotIndex >= 0 ? numberText.Substring(0, dotIndex) : numberText;
            if (!int.TryParse(bytePart, NumberStyles.None, CultureInfo.InvariantCulture, out int byteNumber) ||
                byteNumber > AmMbMaxIndex)
            {
                errorMessage = $"유효하지 않은 AM MX 바이트 번호: {original}";
                return false;
            }

            byte bitOffset = 0;
            if (dotIndex >= 0 &&
                (!byte.TryParse(numberText.Substring(dotIndex + 1), NumberStyles.None, CultureInfo.InvariantCulture, out bitOffset) ||
                 bitOffset > MaxBitIndexInByte))
            {
                errorMessage = $"유효하지 않은 AM MX 비트 번호(0~{MaxBitIndexInByte}): {original}";
                return false;
            }

            ushort wordAddress = (ushort)(byteNumber / BytesPerWord);
            byte bitInWord = (byte)(((byteNumber % BytesPerWord) * BitsPerByte) + bitOffset);
            int linearBitNumber = (byteNumber * BitsPerByte) + bitOffset;
            result = new InovanceAddress(original, InovanceSeries.AM, ModbusArea.HoldingRegister, wordAddress, isBit: true, bitInWord, "MX", linearBitNumber, usesExtendedFunctionCode: false);
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int byteOffset) ||
            byteOffset > AmMbMaxIndex)
        {
            errorMessage = $"유효하지 않은 AM MX 주소 번호: {original}";
            return false;
        }

        int wordIndex = byteOffset / BytesPerWord;
        result = new InovanceAddress(original, InovanceSeries.AM, ModbusArea.HoldingRegister, (ushort)wordIndex, isBit: false, bitIndexInWord: null, "MX", wordIndex, usesExtendedFunctionCode: false);
        return true;
    }

    private static bool ParseAmMb(
        string original,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        string numberText = original.Substring(TwoCharPrefixLength);
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int byteIndex) ||
            byteIndex > AmMbMaxIndex)
        {
            errorMessage = $"유효하지 않은 AM MB 바이트 주소 번호(0~{AmMbMaxIndex}): {original}";
            return false;
        }

        ushort wordAddress = (ushort)(byteIndex / BytesPerWord);
        result = new InovanceAddress(original, InovanceSeries.AM, ModbusArea.HoldingRegister, wordAddress, isBit: false, bitIndexInWord: null, "MB", byteIndex, usesExtendedFunctionCode: false);
        return true;
    }

    private static bool ParseAmWordNumber(
        string original,
        int prefixLength,
        string deviceType,
        int maxValue,
        Func<int, ushort> toPduAddress,
        bool usesExtendedFunctionCode,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        string numberText = original.Substring(prefixLength);
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber) ||
            parsedNumber > maxValue)
        {
            errorMessage = $"AM 주소 범위(0~{maxValue}) 초과 또는 오류: {original}";
            return false;
        }

        result = new InovanceAddress(original, InovanceSeries.AM, ModbusArea.HoldingRegister, toPduAddress(parsedNumber), isBit: false, bitIndexInWord: null, deviceType, parsedNumber, usesExtendedFunctionCode);
        return true;
    }

    private static bool TryParseH3u(
        string trimmed,
        bool isBitAccess,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        char firstChar = trimmed[0];
        if (firstChar is 'X' or 'Y')
        {
            string octalText = trimmed.Substring(SingleCharPrefixLength);
            if (!TryParseOctal(octalText, H3uIoMaxOctal, out ushort octalValue))
            {
                errorMessage = $"유효하지 않은 H3U 8진수 주소(0~377): {trimmed}";
                return false;
            }

            ushort basePdu = firstChar == 'X' ? H3uXBasePdu : H3uYBasePdu;
            return TryCreateAddress(trimmed, InovanceSeries.H3u, ModbusArea.Coil, basePdu + octalValue, isBit: true, firstChar.ToString(), octalValue, out result, out errorMessage);
        }

        if (trimmed.StartsWith("SM", StringComparison.Ordinal) || trimmed.StartsWith("SD", StringComparison.Ordinal))
        {
            bool isSm = trimmed.StartsWith("SM", StringComparison.Ordinal);
            if (!int.TryParse(trimmed.Substring(TwoCharPrefixLength), NumberStyles.None, CultureInfo.InvariantCulture, out int specialNumber) ||
                specialNumber > H3uSmSdMaxIndex)
            {
                errorMessage = $"유효하지 않은 H3U 특수 디바이스 번호(0~{H3uSmSdMaxIndex}): {trimmed}";
                return false;
            }

            ModbusArea specialArea = isSm ? ModbusArea.Coil : ModbusArea.HoldingRegister;
            string deviceType = isSm ? "SM" : "SD";
            return TryCreateAddress(trimmed, InovanceSeries.H3u, specialArea, (long)H3uSmSdBasePdu + specialNumber, isSm, deviceType, specialNumber, out result, out errorMessage);
        }

        return TryParseH3uGeneral(trimmed, firstChar, trimmed.Substring(SingleCharPrefixLength), isBitAccess, out result, out errorMessage);
    }

    private static bool TryParseH3uGeneral(
        string original,
        char firstChar,
        string numberText,
        bool isBitAccess,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber))
        {
            errorMessage = $"유효하지 않은 H3U 주소 번호: {original}";
            return false;
        }

        return firstChar switch
        {
            'M' when parsedNumber <= H3uMMaxIndex =>
                TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.Coil, parsedNumber < H3uMHighBoundary ? parsedNumber : (parsedNumber - H3uMHighBoundary) + H3uMHighBasePdu, isBit: true, "M", parsedNumber, out result, out errorMessage),
            'S' when parsedNumber <= H3uSMaxIndex =>
                TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.Coil, (long)H3uSBasePdu + parsedNumber, isBit: true, "S", parsedNumber, out result, out errorMessage),
            'D' when parsedNumber <= H3uDMaxIndex =>
                TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, parsedNumber, isBit: false, "D", parsedNumber, out result, out errorMessage),
            'R' when parsedNumber <= H3uRMaxIndex =>
                TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, (long)H3uRBasePdu + parsedNumber, isBit: false, "R", parsedNumber, out result, out errorMessage),
            'T' when parsedNumber <= H3uTMaxIndex =>
                TryCreateAddress(original, InovanceSeries.H3u, isBitAccess ? ModbusArea.Coil : ModbusArea.HoldingRegister, (long)H3uTBasePdu + parsedNumber, isBitAccess, "T", parsedNumber, out result, out errorMessage),
            'C' when parsedNumber <= H3uCMaxIndex =>
                TryCreateH3uCounterAddress(original, parsedNumber, isBitAccess, out result, out errorMessage),
            _ => FailAddress(original, "H3U", out errorMessage),
        };
    }

    private static bool TryCreateH3uCounterAddress(
        string original,
        int parsedNumber,
        bool isBitAccess,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        if (isBitAccess)
        {
            return TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.Coil, (long)H3uCBasePdu + parsedNumber, isBit: true, "C", parsedNumber, out result, out errorMessage);
        }

        long counterPdu = parsedNumber < H3uCounter32Start
            ? (long)H3uCBasePdu + parsedNumber
            : (long)H3uCDoubleWordBasePdu + (((long)parsedNumber - H3uCounter32Start) * WordsPer32BitCounter);
        return TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, counterPdu, isBit: false, "C", parsedNumber, out result, out errorMessage);
    }

    private static bool TryParseH5u(
        string trimmed,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        char firstChar = trimmed[0];
        string numberText = trimmed.Substring(SingleCharPrefixLength);

        if (firstChar is 'X' or 'Y')
        {
            if (!TryParseOctal(numberText, H5uIoMaxOctal, out ushort octalValue))
            {
                errorMessage = $"유효하지 않은 H5U 8진수 주소(0~1777): {trimmed}";
                return false;
            }

            ushort basePdu = firstChar == 'X' ? H3uXBasePdu : H3uYBasePdu;
            return TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, basePdu + octalValue, isBit: true, firstChar.ToString(), octalValue, out result, out errorMessage);
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber))
        {
            errorMessage = $"유효하지 않은 H5U 주소 번호: {trimmed}";
            return false;
        }

        return firstChar switch
        {
            'M' when parsedNumber <= H5uMMaxIndex =>
                TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, parsedNumber, isBit: true, "M", parsedNumber, out result, out errorMessage),
            'B' when parsedNumber <= H5uBMaxIndex =>
                TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (long)H5uBBasePdu + parsedNumber, isBit: true, "B", parsedNumber, out result, out errorMessage),
            'S' when parsedNumber <= H5uSMaxIndex =>
                TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (long)H5uSBasePdu + parsedNumber, isBit: true, "S", parsedNumber, out result, out errorMessage),
            'D' when parsedNumber <= H5uDMaxIndex =>
                TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.HoldingRegister, parsedNumber, isBit: false, "D", parsedNumber, out result, out errorMessage),
            'R' when parsedNumber <= H5uRMaxIndex =>
                TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.HoldingRegister, (long)H5uRBasePdu + parsedNumber, isBit: false, "R", parsedNumber, out result, out errorMessage),
            _ => FailAddress(trimmed, "H5U", out errorMessage),
        };
    }

    private static bool TryCreateAddress(
        string original,
        InovanceSeries series,
        ModbusArea area,
        long pduValue,
        bool isBit,
        string deviceType,
        int number,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (pduValue < 0 || pduValue > ushort.MaxValue)
        {
            errorMessage = $"주소({original})가 16비트 PDU 범위(0~{ushort.MaxValue})를 초과합니다.";
            return false;
        }

        result = new InovanceAddress(original, series, area, (ushort)pduValue, isBit, bitIndexInWord: null, deviceType, number, usesExtendedFunctionCode: false);
        return true;
    }

    private static bool FailAddress(string trimmed, string seriesName, out string? errorMessage)
    {
        errorMessage = $"지원하지 않거나 범위를 벗어난 {seriesName} 디바이스입니다: {trimmed}";
        return false;
    }

    private static int GetMaxDeviceIndex(InovanceSeries series, string deviceType, bool isBit) => series switch
    {
        InovanceSeries.AM => deviceType switch
        {
            "MD" => AmMdMaxIndex,
            "MB" => AmMbMaxIndex,
            _ => AmMwMaxIndex,
        },
        InovanceSeries.H3u => deviceType switch
        {
            "X" or "Y" => H3uIoMaxOctal,
            "SM" or "SD" => H3uSmSdMaxIndex,
            "M" => H3uMMaxIndex,
            "S" => H3uSMaxIndex,
            "D" => H3uDMaxIndex,
            "R" => H3uRMaxIndex,
            "T" => H3uTMaxIndex,
            "C" => H3uCMaxIndex,
            _ => 0,
        },
        InovanceSeries.H5u => deviceType switch
        {
            "X" or "Y" => H5uIoMaxOctal,
            "M" => H5uMMaxIndex,
            "B" => H5uBMaxIndex,
            "S" => H5uSMaxIndex,
            "D" => H5uDMaxIndex,
            "R" => H5uRMaxIndex,
            _ => 0,
        },
        _ => isBit ? 0 : 0,
    };

    private static bool TryParseOctal(string text, int maxValue, out ushort result)
    {
        result = 0;
        if (text.Length == 0)
        {
            return false;
        }

        long accumulated = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (character < MinOctalDigit || character > MaxOctalDigit)
            {
                return false;
            }

            accumulated = (accumulated * OctalRadix) + (character - MinOctalDigit);
            if (accumulated > maxValue)
            {
                return false;
            }
        }

        result = (ushort)accumulated;
        return true;
    }
}
