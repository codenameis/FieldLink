using System;
using System.Diagnostics.CodeAnalysis;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Inovance;

/// <summary>Inovance PLC 디바이스 주소를 Modbus PDU 주소로 변환한 값 객체입니다. (사양서 §1)</summary>
public sealed class InovanceAddress
{
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
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        string trimmed = address!.Trim().ToUpperInvariant();
        if (trimmed.StartsWith("%", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(1);
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
            return ParseAmBitArea(trimmed, trimmed.StartsWith("QX", StringComparison.Ordinal) ? 2 : 1, ModbusArea.Coil, out result, out errorMessage);
        }

        if (trimmed.StartsWith("IX", StringComparison.Ordinal) || trimmed.StartsWith("I", StringComparison.Ordinal))
        {
            return ParseAmBitArea(trimmed, trimmed.StartsWith("IX", StringComparison.Ordinal) ? 2 : 1, ModbusArea.DiscreteInput, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MX", StringComparison.Ordinal))
        {
            return ParseAmMx(trimmed, isBitAccess, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MD", StringComparison.Ordinal))
        {
            return ParseAmNumber(trimmed, 2, n => (ushort)(n * 2), ModbusArea.HoldingRegister, false, out result, out errorMessage);
        }

        if (trimmed.StartsWith("MW", StringComparison.Ordinal) || trimmed.StartsWith("M", StringComparison.Ordinal))
        {
            int pLen = trimmed.StartsWith("MW", StringComparison.Ordinal) ? 2 : 1;
            return ParseAmNumber(trimmed, pLen, n => (ushort)n, ModbusArea.HoldingRegister, false, out result, out errorMessage);
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
            if (!int.TryParse(numberText.Substring(0, dot), out int n) || !byte.TryParse(numberText.Substring(dot + 1), out byte b) || b > 7)
            {
                errorMessage = $"유효하지 않은 AM 비트 주소: {original}";
                return false;
            }

            result = new InovanceAddress(original, InovanceSeries.AM, area, (ushort)((n * 8) + b), isBit: true);
            return true;
        }

        if (!int.TryParse(numberText, out int singleNum))
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
        string numberText = original.Substring(2);
        int dot = numberText.IndexOf('.');
        if (dot >= 0 || isBitAccess)
        {
            int byteNum = dot >= 0 ? int.Parse(numberText.Substring(0, dot)) : int.Parse(numberText);
            byte bitNum = dot >= 0 ? byte.Parse(numberText.Substring(dot + 1)) : (byte)0;
            ushort wordAddr = (ushort)(byteNum / 2);
            byte bitInWord = (byte)(((byteNum % 2) * 8) + bitNum);
            result = new InovanceAddress(original, InovanceSeries.AM, ModbusArea.HoldingRegister, wordAddr, isBit: true, bitInWord);
            return true;
        }

        if (!int.TryParse(numberText, out int byteOffset))
        {
            errorMessage = $"유효하지 않은 AM MX 주소: {original}";
            return false;
        }

        result = new InovanceAddress(original, InovanceSeries.AM, ModbusArea.HoldingRegister, (ushort)(byteOffset / 2), isBit: false);
        return true;
    }

    private static bool ParseAmNumber(string original, int prefixLen, Func<int, ushort> pduCalc, ModbusArea area, bool isBit, [NotNullWhen(true)] out InovanceAddress? result, out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        if (!int.TryParse(original.Substring(prefixLen), out int parsedNumber) || parsedNumber < 0 || parsedNumber > 65535)
        {
            errorMessage = $"유효하지 않은 AM 주소 번호: {original}";
            return false;
        }

        result = new InovanceAddress(original, InovanceSeries.AM, area, pduCalc(parsedNumber), isBit);
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
        string numberText = trimmed.Substring(1);

        if (p1 is 'X' or 'Y')
        {
            if (!TryParseOctal(numberText, out ushort octVal))
            {
                errorMessage = $"8진수 주소 파싱 실패: {trimmed}";
                return false;
            }

            ushort basePdu = p1 == 'X' ? (ushort)0xF800 : (ushort)0xFC00;
            result = new InovanceAddress(trimmed, InovanceSeries.H3u, ModbusArea.Coil, (ushort)(basePdu + octVal), isBit: true);
            return true;
        }

        if (trimmed.StartsWith("SM", StringComparison.Ordinal) || trimmed.StartsWith("SD", StringComparison.Ordinal))
        {
            bool isSm = trimmed.StartsWith("SM", StringComparison.Ordinal);
            int n = int.Parse(trimmed.Substring(2));
            ModbusArea a = isSm ? ModbusArea.Coil : ModbusArea.HoldingRegister;
            result = new InovanceAddress(trimmed, InovanceSeries.H3u, a, (ushort)(0x2400 + n), isBit: isSm);
            return true;
        }

        return TryParseH3uGeneral(trimmed, p1, numberText, isBitAccess, out result, out errorMessage);
    }

    private static bool TryParseH3uGeneral(string original, char p1, string numberText, bool isBitAccess, [NotNullWhen(true)] out InovanceAddress? result, out string? errorMessage)
    {
        result = null;
        errorMessage = null;
        if (!int.TryParse(numberText, out int parsedNumber))
        {
            errorMessage = $"유효하지 않은 H3U 주소 번호: {original}";
            return false;
        }

        switch (p1)
        {
            case 'M':
                ushort mPdu = parsedNumber < 8000 ? (ushort)parsedNumber : (ushort)((parsedNumber - 8000) + 0x1F40);
                result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.Coil, mPdu, isBit: true);
                return true;
            case 'S':
                result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.Coil, (ushort)(0xE000 + parsedNumber), isBit: true);
                return true;
            case 'D':
                result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, (ushort)parsedNumber, isBit: false);
                return true;
            case 'R':
                result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, (ushort)(0x3000 + parsedNumber), isBit: false);
                return true;
            case 'T':
                ModbusArea tArea = isBitAccess ? ModbusArea.Coil : ModbusArea.HoldingRegister;
                result = new InovanceAddress(original, InovanceSeries.H3u, tArea, (ushort)(0xF000 + parsedNumber), isBit: isBitAccess);
                return true;
            case 'C':
                if (isBitAccess)
                {
                    result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.Coil, (ushort)(0xF400 + parsedNumber), isBit: true);
                    return true;
                }

                ushort cPdu = parsedNumber < 200 ? (ushort)(0xF400 + parsedNumber) : (ushort)(0xF700 + ((parsedNumber - 200) * 2));
                result = new InovanceAddress(original, InovanceSeries.H3u, ModbusArea.HoldingRegister, cPdu, isBit: false);
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
        string numberText = trimmed.Substring(1);

        if (p1 is 'X' or 'Y')
        {
            if (!TryParseOctal(numberText, out ushort octVal))
            {
                errorMessage = $"8진수 주소 파싱 실패: {trimmed}";
                return false;
            }

            ushort basePdu = p1 == 'X' ? (ushort)0xF800 : (ushort)0xFC00;
            result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (ushort)(basePdu + octVal), isBit: true);
            return true;
        }

        if (!int.TryParse(numberText, out int parsedNumber))
        {
            errorMessage = $"유효하지 않은 H5U 주소 번호: {trimmed}";
            return false;
        }

        switch (p1)
        {
            case 'M':
                result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (ushort)parsedNumber, isBit: true);
                return true;
            case 'B':
                result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (ushort)(0x3000 + parsedNumber), isBit: true);
                return true;
            case 'S':
                result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.Coil, (ushort)(0xE000 + parsedNumber), isBit: true);
                return true;
            case 'D':
                result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.HoldingRegister, (ushort)parsedNumber, isBit: false);
                return true;
            case 'R':
                result = new InovanceAddress(trimmed, InovanceSeries.H5u, ModbusArea.HoldingRegister, (ushort)(0x3000 + parsedNumber), isBit: false);
                return true;
            default:
                errorMessage = $"지원하지 않는 H5U 디바이스입니다: {trimmed}";
                return false;
        }
    }

    private static bool TryParseOctal(string text, out ushort result)
    {
        result = 0;
        foreach (char c in text)
        {
            if (c is < '0' or > '7')
            {
                return false;
            }
        }

        try
        {
            result = Convert.ToUInt16(text, 8);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="InovanceAddress"/>를 반환합니다.</summary>
    public InovanceAddress Offset(int count)
    {
        ushort newPdu = (ushort)(PduAddress + count);
        return new InovanceAddress($"{OriginalAddress}+{count}", Series, Area, newPdu, IsBit, BitIndexInWord);
    }
}
