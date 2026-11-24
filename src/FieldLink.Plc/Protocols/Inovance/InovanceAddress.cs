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
    private const int AmMdMaxIndex = 32767;
    private const int AmMwMaxIndex = 65535;
    private const ushort H3uXBasePdu = 0xF800;
    private const ushort H3uYBasePdu = 0xFC00;
    private const ushort H3uSmSdBasePdu = 0x2400;
    private const int H3uMHighBoundary = 8000;
    private const ushort H3uMHighBasePdu = 0x1F40;
    private const ushort H3uSBasePdu = 0xE000;
    private const ushort H3uRBasePdu = 0x3000;
    private const ushort H3uTBasePdu = 0xF000;
    private const ushort H3uCBasePdu = 0xF400;
    private const int H3uCounter32Start = 200;
    private const ushort H3uCDoubleWordBasePdu = 0xF700;
    private const ushort H5uBBasePdu = 0x3000;
    private const ushort H5uSBasePdu = 0xE000;
    private const ushort H5uRBasePdu = 0x3000;
    private const char MinOctalDigit = '0';
    private const char MaxOctalDigit = '7';
    private const char BitSeparator = '.';

    private InovanceAddress(
        string originalAddress,
        InovanceSeries series,
        ModbusArea area,
        ushort pduAddress,
        bool isBit,
        byte? bitIndexInWord = null,
        string deviceType = "",
        int number = 0)
    {
        OriginalAddress = originalAddress;
        Series = series;
        Area = area;
        PduAddress = pduAddress;
        IsBit = isBit;
        BitIndexInWord = bitIndexInWord;
        DeviceType = deviceType;
        Number = number;
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

    /// <summary>디바이스 종류 문자열입니다. (예: "D", "M", "C")</summary>
    public string DeviceType { get; }

    /// <summary>파싱된 디바이스 번호입니다.</summary>
    public int Number { get; }

    /// <summary>32비트 워드 디바이스(AM MD 또는 H3U C200~C255) 여부입니다.</summary>
    public bool Is32BitWord =>
        !IsBit &&
        ((Series == InovanceSeries.AM && DeviceType == "MD") ||
         (Series == InovanceSeries.H3u && DeviceType == "C" && Number >= H3uCounter32Start));

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static InovanceAddress Parse(string address, InovanceSeries series, bool isBitAccess = false)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
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

    private static bool TryParseAm(
        string trimmed,
        bool isBitAccess,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (trimmed.StartsWith("QX", StringComparison.Ordinal) || trimmed.StartsWith("Q", StringComparison.Ordinal))
        {
            int prefixLen = trimmed.StartsWith("QX", StringComparison.Ordinal) ? TwoCharPrefixLength : SingleCharPrefixLength;
            return ParseAmBitArea(trimmed, prefixLen, ModbusArea.Coil, out result, out errorMessage);
        }

        if (trimmed.StartsWith("IX", StringComparison.Ordinal) || trimmed.StartsWith("I", StringComparison.Ordinal))
        {
            int prefixLen = trimmed.StartsWith("IX", StringComparison.Ordinal) ? TwoCharPrefixLength : SingleCharPrefixLength;
            return ParseAmBitArea(trimmed, prefixLen, ModbusArea.DiscreteInput, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MX", StringComparison.Ordinal))
        {
            return ParseAmMx(trimmed, isBitAccess, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MD", StringComparison.Ordinal))
        {
            return ParseAmNumber(trimmed, TwoCharPrefixLength, "MD", AmMdMaxIndex, n => (ushort)(n * WordsPer32BitCounter), ModbusArea.HoldingRegister, isBit: false, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MW", StringComparison.Ordinal) || trimmed.StartsWith("M", StringComparison.Ordinal))
        {
            int prefixLen = trimmed.StartsWith("MW", StringComparison.Ordinal) ? TwoCharPrefixLength : SingleCharPrefixLength;
            return ParseAmNumber(trimmed, prefixLen, "MW", AmMwMaxIndex, n => (ushort)n, ModbusArea.HoldingRegister, isBit: false, out result, out errorMessage);
        }

        errorMessage = $"지원하지 않는 Inovance AM 주소입니다: {trimmed}";
        return false;
    }

    private static bool ParseAmBitArea(
        string original,
        int prefixLen,
        ModbusArea area,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        string numberText = original.Substring(prefixLen);
        int dot = numberText.IndexOf(BitSeparator);
        if (dot >= 0)
        {
            if (!int.TryParse(numberText.Substring(0, dot), NumberStyles.None, CultureInfo.InvariantCulture, out int byteIndex) ||
                !byte.TryParse(numberText.Substring(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out byte bitOffset) ||
                bitOffset > MaxBitIndexInByte ||
                ((long)byteIndex * BitsPerByte) + bitOffset > ushort.MaxValue)
            {
                errorMessage = $"유효하지 않은 AM 비트 주소: {original}";
                return false;
            }

            ushort bitPdu = (ushort)((byteIndex * BitsPerByte) + bitOffset);
            result = new InovanceAddress(original, InovanceSeries.AM, area, bitPdu, isBit: true);
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int singleNum) ||
            singleNum > ushort.MaxValue)
        {
            errorMessage = $"유효하지 않은 AM 주소 번호: {original}";
            return false;
        }

        result = new InovanceAddress(original, InovanceSeries.AM, area, (ushort)singleNum, isBit: true);
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
        int dot = numberText.IndexOf(BitSeparator);
        if (dot >= 0 || isBitAccess)
        {
            string bytePart = dot >= 0 ? numberText.Substring(0, dot) : numberText;
            if (!int.TryParse(bytePart, NumberStyles.None, CultureInfo.InvariantCulture, out int byteNum))
            {
                errorMessage = $"유효하지 않은 AM MX 바이트 번호: {original}";
                return false;
            }

            byte bitNum = 0;
            if (dot >= 0 &&
                (!byte.TryParse(numberText.Substring(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out bitNum) || bitNum > MaxBitIndexInByte))
            {
                errorMessage = $"유효하지 않은 AM MX 비트 번호(0~{MaxBitIndexInByte}): {original}";
                return false;
            }

            int wordIndex = byteNum / BytesPerWord;
            if (wordIndex > ushort.MaxValue)
            {
                errorMessage = $"AM MX 주소 범위 초과: {original}";
                return false;
            }

            ushort wordAddr = (ushort)wordIndex;
            byte bitInWord = (byte)(((byteNum % BytesPerWord) * BitsPerByte) + bitNum);
            result = new InovanceAddress(original, InovanceSeries.AM, ModbusArea.HoldingRegister, wordAddr, isBit: true, bitInWord);
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int byteOffset) ||
            byteOffset / BytesPerWord > ushort.MaxValue)
        {
            errorMessage = $"유효하지 않은 AM MX 주소 번호: {original}";
            return false;
        }

        result = new InovanceAddress(original, InovanceSeries.AM, ModbusArea.HoldingRegister, (ushort)(byteOffset / BytesPerWord), isBit: false);
        return true;
    }

    private static bool ParseAmNumber(
        string original,
        int prefixLen,
        string deviceType,
        int maxVal,
        Func<int, ushort> toPdu,
        ModbusArea area,
        bool isBit,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        string numberText = original.Substring(prefixLen);
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber) ||
            parsedNumber < 0 ||
            parsedNumber > maxVal)
        {
            errorMessage = $"AM 주소 범위(0~{maxVal}) 초과 또는 오류: {original}";
            return false;
        }

        result = new InovanceAddress(original, InovanceSeries.AM, area, toPdu(parsedNumber), isBit, deviceType: deviceType, number: parsedNumber);
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

        char p1 = trimmed[0];
        string numberText = trimmed.Substring(SingleCharPrefixLength);

        if (p1 is 'X' or 'Y')
        {
            if (!TryParseOctal(numberText, out ushort octVal))
            {
                errorMessage = $"8진수 주소 파싱 실패: {trimmed}";
                return false;
            }

            ushort basePdu = p1 == 'X' ? H3uXBasePdu : H3uYBasePdu;
            if ((long)basePdu + octVal > ushort.MaxValue)
            {
                errorMessage = $"H3U {p1} 주소 범위 초과: {trimmed}";
                return false;
            }

            result = new InovanceAddress(trimmed, InovanceSeries.H3u, ModbusArea.Coil, (ushort)(basePdu + octVal), isBit: true);
            return true;
        }

        if (trimmed.StartsWith("SM", StringComparison.Ordinal) || trimmed.StartsWith("SD", StringComparison.Ordinal))
        {
            bool isSm = trimmed.StartsWith("SM", StringComparison.Ordinal);
            if (!int.TryParse(trimmed.Substring(TwoCharPrefixLength), NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber) ||
                (long)H3uSmSdBasePdu + parsedNumber > ushort.MaxValue)
            {
                errorMessage = $"유효하지 않은 H3U 주소 번호: {trimmed}";
                return false;
            }

            ModbusArea area = isSm ? ModbusArea.Coil : ModbusArea.HoldingRegister;
            result = new InovanceAddress(trimmed, InovanceSeries.H3u, area, (ushort)(H3uSmSdBasePdu + parsedNumber), isBit: isSm);
            return true;
        }

        return TryParseH3uGeneral(trimmed, p1, numberText, isBitAccess, out result, out errorMessage);
    }

    private static bool TryParseH3uGeneral(
        string original,
        char p1,
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

        switch (p1)
        {
            case 'M':
                long mPdu = parsedNumber < H3uMHighBoundary ? parsedNumber : (parsedNumber - H3uMHighBoundary) + H3uMHighBasePdu;
                return TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.Coil, mPdu, isBit: true, "M", parsedNumber, out result, out errorMessage);
            case 'S':
                return TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.Coil, (long)H3uSBasePdu + parsedNumber, isBit: true, "S", parsedNumber, out result, out errorMessage);
            case 'D':
                return TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, parsedNumber, isBit: false, "D", parsedNumber, out result, out errorMessage);
            case 'R':
                return TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, (long)H3uRBasePdu + parsedNumber, isBit: false, "R", parsedNumber, out result, out errorMessage);
            case 'T':
                ModbusArea tArea = isBitAccess ? ModbusArea.Coil : ModbusArea.HoldingRegister;
                return TryCreateAddress(original, InovanceSeries.H3u, tArea, (long)H3uTBasePdu + parsedNumber, isBit: isBitAccess, "T", parsedNumber, out result, out errorMessage);
            case 'C':
                if (isBitAccess)
                {
                    return TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.Coil, (long)H3uCBasePdu + parsedNumber, isBit: true, "C", parsedNumber, out result, out errorMessage);
                }

                long cPdu = parsedNumber < H3uCounter32Start
                    ? (long)H3uCBasePdu + parsedNumber
                    : (long)H3uCDoubleWordBasePdu + (((long)parsedNumber - H3uCounter32Start) * WordsPer32BitCounter);
                return TryCreateAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, cPdu, isBit: false, "C", parsedNumber, out result, out errorMessage);
            default:
                errorMessage = $"지원하지 않는 H3U 디바이스입니다: {original}";
                return false;
        }
    }

    private static bool TryParseH5u(
        string trimmed,
        [NotNullWhen(true)] out InovanceAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        char p1 = trimmed[0];
        string numberText = trimmed.Substring(SingleCharPrefixLength);

        if (p1 is 'X' or 'Y')
        {
            if (!TryParseOctal(numberText, out ushort octVal))
            {
                errorMessage = $"8진수 주소 파싱 실패: {trimmed}";
                return false;
            }

            ushort basePdu = p1 == 'X' ? H3uXBasePdu : H3uYBasePdu;
            if ((long)basePdu + octVal > ushort.MaxValue)
            {
                errorMessage = $"H5U {p1} 주소 범위 초과: {trimmed}";
                return false;
            }

            result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (ushort)(basePdu + octVal), isBit: true);
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber))
        {
            errorMessage = $"유효하지 않은 H5U 주소 번호: {trimmed}";
            return false;
        }

        return p1 switch
        {
            'M' => TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, parsedNumber, isBit: true, "M", parsedNumber, out result, out errorMessage),
            'B' => TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (long)H5uBBasePdu + parsedNumber, isBit: true, "B", parsedNumber, out result, out errorMessage),
            'S' => TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (long)H5uSBasePdu + parsedNumber, isBit: true, "S", parsedNumber, out result, out errorMessage),
            'D' => TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.HoldingRegister, parsedNumber, isBit: false, "D", parsedNumber, out result, out errorMessage),
            'R' => TryCreateAddress(trimmed, InovanceSeries.H5u, ModbusArea.HoldingRegister, (long)H5uRBasePdu + parsedNumber, isBit: false, "R", parsedNumber, out result, out errorMessage),
            _ => FailH5uPrefix(trimmed, out errorMessage),
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

        result = new InovanceAddress(original, series, area, (ushort)pduValue, isBit, deviceType: deviceType, number: number);
        return true;
    }

    private static bool FailH5uPrefix(string trimmed, out string? errorMessage)
    {
        errorMessage = $"지원하지 않는 H5U 디바이스입니다: {trimmed}";
        return false;
    }

    private static bool TryParseOctal(string text, out ushort result)
    {
        result = 0;
        if (text.Length == 0)
        {
            return false;
        }

        long accumulated = 0;
        foreach (char c in text)
        {
            if (c < MinOctalDigit || c > MaxOctalDigit)
            {
                return false;
            }

            accumulated = (accumulated * OctalRadix) + (c - MinOctalDigit);
            if (accumulated > ushort.MaxValue)
            {
                return false;
            }
        }

        result = (ushort)accumulated;
        return true;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="InovanceAddress"/>를 반환합니다.</summary>
    public InovanceAddress Offset(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        if (BitIndexInWord.HasValue)
        {
            int totalBitIndex = checked((PduAddress * BitsPerWord) + BitIndexInWord.Value + count);
            ushort nextWordPdu = checked((ushort)(totalBitIndex / BitsPerWord));
            byte nextBitInWord = (byte)(totalBitIndex % BitsPerWord);
            return new InovanceAddress($"{OriginalAddress}+{count}", Series, Area, nextWordPdu, IsBit, nextBitInWord, DeviceType, checked(Number + count));
        }

        int pduStep = Is32BitWord ? checked(count * WordsPer32BitCounter) : count;
        ushort newPdu = checked((ushort)(PduAddress + pduStep));
        return new InovanceAddress($"{OriginalAddress}+{count}", Series, Area, newPdu, IsBit, BitIndexInWord, DeviceType, checked(Number + count));
    }
}
