using System;
using System.Diagnostics.CodeAnalysis;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.Plc.Protocols.Megmeet;

/// <summary>Megmeet PLC 디바이스 주소를 Modbus PDU 주소로 변환한 값 객체입니다. (사양서 §1)</summary>
public sealed class MegmeetAddress
{
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

    /// <summary>디바이스 인덱스 번호입니다.</summary>
    public int Number { get; }

    /// <summary>매핑되는 Modbus 메모리 영역입니다.</summary>
    public ModbusArea Area { get; }

    /// <summary>Modbus PDU 16비트 주소입니다.</summary>
    public ushort PduAddress { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static MegmeetAddress Parse(string address, bool isBitAccess = false)
    {
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
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
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

    private static bool TryExtractDevice(string trimmed, out string devicePrefix, out string numberText)
    {
        devicePrefix = string.Empty;
        numberText = string.Empty;

        string[] twoCharPrefixes = ["SM", "SD"];
        foreach (string d in twoCharPrefixes)
        {
            if (trimmed.StartsWith(d, StringComparison.Ordinal))
            {
                devicePrefix = d;
                numberText = trimmed.Substring(2);
                return true;
            }
        }

        char p1 = trimmed[0];
        devicePrefix = p1.ToString();
        numberText = trimmed.Substring(1);
        return p1 is 'X' or 'Y' or 'M' or 'S' or 'T' or 'C' or 'D' or 'Z' or 'R';
    }

    private static bool TryParseOctal(string original, string devicePrefix, string numberText, [NotNullWhen(true)] out MegmeetAddress? result, out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        foreach (char c in numberText)
        {
            if (c is < '0' or > '7')
            {
                errorMessage = $"8진수 주소 파싱 실패: {original}";
                return false;
            }
        }

        try
        {
            ushort octVal = Convert.ToUInt16(numberText, 8);
            ModbusArea area = devicePrefix == "X" ? ModbusArea.DiscreteInput : ModbusArea.Coil;
            result = new MegmeetAddress(original, devicePrefix, octVal, area, octVal, isBit: true);
            return true;
        }
        catch
        {
            errorMessage = $"8진수 변환 실패: {original}";
            return false;
        }
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

        if (!int.TryParse(numberText, out int parsedNumber) || parsedNumber < 0)
        {
            errorMessage = $"유효하지 않은 Megmeet 디바이스 번호: {original}";
            return false;
        }

        bool isBit = isBitAccess || devicePrefix is "M" or "SM" or "S";
        ModbusArea area = isBit ? ModbusArea.Coil : ModbusArea.HoldingRegister;
        ushort pdu = CalculatePdu(devicePrefix, parsedNumber, isBit);

        result = new MegmeetAddress(original, devicePrefix, parsedNumber, area, pdu, isBit);
        return true;
    }

    private static ushort CalculatePdu(string devicePrefix, int n, bool isBit) => devicePrefix switch
    {
        "M" => (ushort)(n < 2048 ? n + 2000 : (n - 2048) + 12000),
        "SM" => (ushort)(n < 256 ? n + 4400 : (n - 256) + 30000),
        "S" => (ushort)(n < 1024 ? n + 6000 : (n - 1024) + 31000),
        "D" => (ushort)n,
        "SD" => (ushort)(n < 256 ? n + 8000 : (n - 256) + 12000),
        "Z" => (ushort)(n + 8500),
        "R" => (ushort)(n + 13000),
        "T" => isBit ? (ushort)(n < 256 ? n + 8000 : (n - 256) + 11000) : (ushort)(n < 256 ? n + 9000 : (n - 256) + 11000),
        "C" => isBit ? (ushort)(n < 256 ? n + 9200 : (n - 256) + 10000) : CalculateCounterWordPdu(n),
        _ => throw new InvalidOperationException($"미지원 디바이스: {devicePrefix}"),
    };

    private static ushort CalculateCounterWordPdu(int n)
    {
        if (n < 200)
        {
            return (ushort)(n + 9500);
        }

        if (n < 256)
        {
            return (ushort)(((n - 200) * 2) + 9700);
        }

        return (ushort)(((n - 256) * 2) + 10000);
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="MegmeetAddress"/>를 반환합니다.</summary>
    public MegmeetAddress Offset(int count)
    {
        int newNum = Number + count;
        ushort newPdu = CalculatePdu(Device, newNum, IsBit);
        return new MegmeetAddress($"{OriginalAddress}+{count}", Device, newNum, Area, newPdu, IsBit);
    }
}
