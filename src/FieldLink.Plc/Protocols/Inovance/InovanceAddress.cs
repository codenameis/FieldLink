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
    private const int MaxBitIndexInByte = 7;
    private const int SingleCharPrefixLength = 1;
    private const int TwoCharPrefixLength = 2;
    private const ushort H3uXBasePdu = 0xF800;
    private const ushort H3uYBasePdu = 0xFC00;
    private const ushort H3uSmSdBasePdu = 0x2400;
    private const int H3uMHighBoundary = 8000;
    private const ushort H3uMHighBasePdu = 0x1F40;
    private const ushort H3uSBasePdu = 0xE000;
    private const ushort H3uRBasePdu = 0x3000;
    private const ushort H3uTBasePdu = 0xF000;
    private const ushort H3uCBasePdu = 0xF400;
    private const int H3uCWordBoundary = 200;
    private const ushort H3uCDoubleWordBasePdu = 0xF700;
    private const ushort H5uBBasePdu = 0x3000;
    private const ushort H5uSBasePdu = 0xE000;
    private const ushort H5uRBasePdu = 0x3000;
    private const char MinOctalDigit = '0';
    private const char MaxOctalDigit = '7';

    private InovanceAddress(
        string originalAddress,
        InovanceSeries series,
        ModbusArea area,
        ushort pduAddress,
        bool isBit,
        byte? bitIndexInWord = null)
    {
        OriginalAddress = originalAddress;
        Series = series;
        Area = area;
        PduAddress = pduAddress;
        IsBit = isBit;
        BitIndexInWord = bitIndexInWord;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string OriginalAddress { get; }

    /// <summary>PLC 시리즈 구분입니다.</summary>
    public InovanceSeries Series { get; }

    /// <summary>매핑되는 Modbus 영역입니다.</summary>
    public ModbusArea Area { get; }

    /// <summary>Modbus PDU 16비트 주소입니다.</summary>
    public ushort PduAddress { get; }

    /// <summary>비트 디바이스 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>워드 내 비트 인덱스 (AM %MX 접근 시 0~15)입니다.</summary>
    public byte? BitIndexInWord { get; }

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

        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            errorMessage = "주소 문자열이 비어 있습니다.";
            return false;
        }

        string trimmed = address.Trim().ToUpperInvariant();
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
            _ => FailUnsupportedSeries(series, out errorMessage),
        };
    }

    private static bool FailUnsupportedSeries(InovanceSeries series, out string? errorMessage)
    {
        errorMessage = $"지원하지 않는 Inovance 시리즈입니다: {series}";
        return false;
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
            return ParseAmBitArea(trimmed, trimmed.StartsWith("QX", StringComparison.Ordinal) ? TwoCharPrefixLength : SingleCharPrefixLength, ModbusArea.Coil, out result, out errorMessage);
        }

        if (trimmed.StartsWith("IX", StringComparison.Ordinal) || trimmed.StartsWith("I", StringComparison.Ordinal))
        {
            return ParseAmBitArea(trimmed, trimmed.StartsWith("IX", StringComparison.Ordinal) ? TwoCharPrefixLength : SingleCharPrefixLength, ModbusArea.DiscreteInput, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MX", StringComparison.Ordinal))
        {
            return ParseAmMx(trimmed, isBitAccess, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MD", StringComparison.Ordinal))
        {
            return ParseAmNumber(trimmed, TwoCharPrefixLength, BytesPerWord, ModbusArea.HoldingRegister, false, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MW", StringComparison.Ordinal) || trimmed.StartsWith("M", StringComparison.Ordinal))
        {
            int pLen = trimmed.StartsWith("MW", StringComparison.Ordinal) ? TwoCharPrefixLength : SingleCharPrefixLength;
            return ParseAmNumber(trimmed, pLen, 1, ModbusArea.HoldingRegister, false, out result, out errorMessage);
        }

        errorMessage = $"지원하지 않는 Inovance AM 주소입니다: {trimmed}";
        return false;
    }

    private static bool ParseAmBitArea(string original, int prefixLen, ModbusArea area, [NotNullWhen(true)] out InovanceAddress? result, out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        string numberText = original.Substring(prefixLen);
        int dot = numberText.IndexOf('.');
        if (dot >= 0)
        {
            if (!int.TryParse(numberText.Substring(0, dot), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ||
                !byte.TryParse(numberText.Substring(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out byte b) ||
                b > MaxBitIndexInByte ||
                ((long)n * BitsPerByte) + b > ushort.MaxValue)
            {
                errorMessage = $"유효하지 않은 AM 비트 주소: {original}";
                return false;
            }

            result = new InovanceAddress(original, InovanceSeries.AM, area, (ushort)((n * BitsPerByte) + b), isBit: true);
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int singleNum) || singleNum > ushort.MaxValue)
        {
            errorMessage = $"유효하지 않은 AM 주소 번호: {original}";
            return false;
        }

        result = new InovanceAddress(original, InovanceSeries.AM, area, (ushort)singleNum, isBit: true);
        return true;
    }

    private static bool ParseAmMx(string original, bool isBitAccess, [NotNullWhen(true)] out InovanceAddress? result, out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        string numberText = original.Substring(TwoCharPrefixLength);
        int dot = numberText.IndexOf('.');
        if (dot >= 0 || isBitAccess)
        {
            string bytePart = dot >= 0 ? numberText.Substring(0, dot) : numberText;
            if (!int.TryParse(bytePart, NumberStyles.None, CultureInfo.InvariantCulture, out int byteNum) || byteNum / BytesPerWord > ushort.MaxValue)
            {
                errorMessage = $"유효하지 않은 AM MX 주소: {original}";
                return false;
            }

            byte bitNum = 0;
            if (dot >= 0 && (!byte.TryParse(numberText.Substring(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out bitNum) || bitNum > MaxBitIndexInByte))
            {
                errorMessage = $"유효하지 않은 AM MX 비트 인덱스: {original}";
                return false;
            }

            ushort wordAddr = (ushort)(byteNum / BytesPerWord);
            byte bitInWord = (byte)(((byteNum % BytesPerWord) * BitsPerByte) + bitNum);
            result = new InovanceAddress(original, InovanceSeries.AM, ModbusArea.HoldingRegister, wordAddr, isBit: true, bitInWord);
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int byteOffset) || byteOffset / BytesPerWord > ushort.MaxValue)
        {
            errorMessage = $"유효하지 않은 AM MX 주소: {original}";
            return false;
        }

        result = new InovanceAddress(original, InovanceSeries.AM, ModbusArea.HoldingRegister, (ushort)(byteOffset / BytesPerWord), isBit: false);
        return true;
    }

    private static bool ParseAmNumber(string original, int prefixLen, int multiplier, ModbusArea area, bool isBit, [NotNullWhen(true)] out InovanceAddress? result, out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        if (!int.TryParse(original.Substring(prefixLen), NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber)
            || parsedNumber < 0
            || (long)parsedNumber * multiplier > ushort.MaxValue)
        {
            errorMessage = $"유효하지 않은 AM 주소 번호: {original}";
            return false;
        }

        result = new InovanceAddress(original, InovanceSeries.AM, area, (ushort)(parsedNumber * multiplier), isBit);
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
            result = new InovanceAddress(trimmed, InovanceSeries.H3u, ModbusArea.Coil, (ushort)(basePdu + octVal), isBit: true);
            return true;
        }

        if (trimmed.StartsWith("SM", StringComparison.Ordinal) || trimmed.StartsWith("SD", StringComparison.Ordinal))
        {
            bool isSm = trimmed.StartsWith("SM", StringComparison.Ordinal);
            if (!int.TryParse(trimmed.Substring(TwoCharPrefixLength), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ||
                H3uSmSdBasePdu + (long)n > ushort.MaxValue)
            {
                errorMessage = $"유효하지 않은 H3U 특수 디바이스 주소: {trimmed}";
                return false;
            }

            ModbusArea a = isSm ? ModbusArea.Coil : ModbusArea.HoldingRegister;
            result = new InovanceAddress(trimmed, InovanceSeries.H3u, a, (ushort)(H3uSmSdBasePdu + n), isBit: isSm);
            return true;
        }

        return TryParseH3uGeneral(trimmed, p1, numberText, isBitAccess, out result, out errorMessage);
    }

    private static bool TryParseH3uGeneral(string original, char p1, string numberText, bool isBitAccess, [NotNullWhen(true)] out InovanceAddress? result, out string? errorMessage)
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
                long mPduLong = parsedNumber < H3uMHighBoundary ? parsedNumber : (parsedNumber - (long)H3uMHighBoundary) + H3uMHighBasePdu;
                if (mPduLong > ushort.MaxValue)
                {
                    errorMessage = $"H3U M 주소 범위를 초과했습니다: {original}";
                    return false;
                }

                result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.Coil, (ushort)mPduLong, isBit: true);
                return true;
            case 'S':
                if (H3uSBasePdu + (long)parsedNumber > ushort.MaxValue)
                {
                    errorMessage = $"H3U S 주소 범위를 초과했습니다: {original}";
                    return false;
                }

                result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.Coil, (ushort)(H3uSBasePdu + parsedNumber), isBit: true);
                return true;
            case 'D':
                if (parsedNumber > ushort.MaxValue)
                {
                    errorMessage = $"H3U D 주소 범위를 초과했습니다: {original}";
                    return false;
                }

                result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, (ushort)parsedNumber, isBit: false);
                return true;
            case 'R':
                if (H3uRBasePdu + (long)parsedNumber > ushort.MaxValue)
                {
                    errorMessage = $"H3U R 주소 범위를 초과했습니다: {original}";
                    return false;
                }

                result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, (ushort)(H3uRBasePdu + parsedNumber), isBit: false);
                return true;
            case 'T':
                if (H3uTBasePdu + (long)parsedNumber > ushort.MaxValue)
                {
                    errorMessage = $"H3U T 주소 범위를 초과했습니다: {original}";
                    return false;
                }

                ModbusArea tArea = isBitAccess ? ModbusArea.Coil : ModbusArea.HoldingRegister;
                result = new InovanceAddress(original, InovanceSeries.H3u, tArea, (ushort)(H3uTBasePdu + parsedNumber), isBit: isBitAccess);
                return true;
            case 'C':
                if (isBitAccess)
                {
                    if (H3uCBasePdu + (long)parsedNumber > ushort.MaxValue)
                    {
                        errorMessage = $"H3U C 비트 주소 범위를 초과했습니다: {original}";
                        return false;
                    }

                    result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.Coil, (ushort)(H3uCBasePdu + parsedNumber), isBit: true);
                    return true;
                }

                long cPduLong = parsedNumber < H3uCWordBoundary
                    ? H3uCBasePdu + (long)parsedNumber
                    : H3uCDoubleWordBasePdu + (((long)parsedNumber - H3uCWordBoundary) * BytesPerWord);
                if (cPduLong > ushort.MaxValue)
                {
                    errorMessage = $"H3U C 워드 주소 범위를 초과했습니다: {original}";
                    return false;
                }

                result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, (ushort)cPduLong, isBit: false);
                return true;
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
            result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (ushort)(basePdu + octVal), isBit: true);
            return true;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedNumber))
        {
            errorMessage = $"유효하지 않은 H5U 주소 번호: {trimmed}";
            return false;
        }

        switch (p1)
        {
            case 'M':
                if (parsedNumber > ushort.MaxValue)
                {
                    errorMessage = $"H5U M 주소 범위를 초과했습니다: {trimmed}";
                    return false;
                }

                result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (ushort)parsedNumber, isBit: true);
                return true;
            case 'B':
                if (H5uBBasePdu + (long)parsedNumber > ushort.MaxValue)
                {
                    errorMessage = $"H5U B 주소 범위를 초과했습니다: {trimmed}";
                    return false;
                }

                result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (ushort)(H5uBBasePdu + parsedNumber), isBit: true);
                return true;
            case 'S':
                if (H5uSBasePdu + (long)parsedNumber > ushort.MaxValue)
                {
                    errorMessage = $"H5U S 주소 범위를 초과했습니다: {trimmed}";
                    return false;
                }

                result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (ushort)(H5uSBasePdu + parsedNumber), isBit: true);
                return true;
            case 'D':
                if (parsedNumber > ushort.MaxValue)
                {
                    errorMessage = $"H5U D 주소 범위를 초과했습니다: {trimmed}";
                    return false;
                }

                result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.HoldingRegister, (ushort)parsedNumber, isBit: false);
                return true;
            case 'R':
                if (H5uRBasePdu + (long)parsedNumber > ushort.MaxValue)
                {
                    errorMessage = $"H5U R 주소 범위를 초과했습니다: {trimmed}";
                    return false;
                }

                result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.HoldingRegister, (ushort)(H5uRBasePdu + parsedNumber), isBit: false);
                return true;
            default:
                errorMessage = $"지원하지 않는 H5U 디바이스입니다: {trimmed}";
                return false;
        }
    }

    private static bool TryParseOctal(string text, out ushort result)
    {
        result = 0;
        if (text.Length == 0)
        {
            return false;
        }

        int value = 0;
        foreach (char c in text)
        {
            if (c < MinOctalDigit || c > MaxOctalDigit)
            {
                return false;
            }

            if (value > (ushort.MaxValue - (c - MinOctalDigit)) / OctalRadix)
            {
                return false;
            }

            value = (value * OctalRadix) + (c - MinOctalDigit);
        }

        result = (ushort)value;
        return true;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="InovanceAddress"/>를 반환합니다.</summary>
    public InovanceAddress Offset(int count)
    {
        if (count < 0 || PduAddress + (long)count > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "이동한 주소가 Modbus 16비트 주소 범위를 초과합니다.");
        }

        ushort newPdu = (ushort)(PduAddress + count);
        return new InovanceAddress($"{OriginalAddress}+{count}", Series, Area, newPdu, IsBit, BitIndexInWord);
    }
}
