using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Xinje;

/// <summary>XINJE PLC 디바이스 주소 표현 및 Modbus/확장 매핑입니다. (사양서 Part A §A.1.4, Part B)</summary>
public sealed class XinjeAddress
{
    private const int OctalRadix = 8;
    private const int HighByteShift = 16;
    private const int MidByteShift = 8;
    private const int ByteMask = 0xFF;
    private const int ExtendedAddressThreshold = 20480;
    private const int Max24BitAddressNumber = 0xFFFFFF;
    private const char MinOctalDigit = '0';
    private const char MaxOctalDigit = '7';
    private const int XcHighAddressBoundary = 8000;
    private const int XdBitIoBlock1Boundary = 0x1000;
    private const int XdBitIoBlock2Boundary = 0x2000;
    private const int XdBitIoBlock3Boundary = 0x3000;
    private const int XdWordIoBlock1Boundary = 10000;
    private const int XdWordIoBlock2Boundary = 20000;
    private const int XdWordIoBlock3Boundary = 30000;

    private static readonly string[] OrderedDevicePrefixes =
    [
        "HSCD", "HSC", "HSD", "ETD", "HTD", "HCD", "SFD", "SEM", "FSD",
        "ET", "HM", "HS", "HT", "HC", "SM", "TD", "CD", "SD", "ID", "QD", "HD", "FD", "FS", "ED",
        "X", "Y", "M", "S", "T", "C", "D", "F", "E"
    ];

    private XinjeAddress(
        string original,
        string device,
        int number,
        byte extendedDeviceCode,
        bool isBit,
        XinjeSeries series)
    {
        Original = original;
        Device = device;
        Number = number;
        ExtendedDeviceCode = extendedDeviceCode;
        IsBit = isBit;
        Series = series;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>디바이스 접두사입니다. (예: "D", "M", "X", "Y", "HD", "ID")</summary>
    public string Device { get; }

    /// <summary>파싱된 절대 번호입니다. (X/Y는 8진수 환산 정수, 그 외는 10진수)</summary>
    public int Number { get; }

    /// <summary>확장 Modbus 1바이트 디바이스 코드입니다. (§A.1.4)</summary>
    public byte ExtendedDeviceCode { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>PLC 시리즈입니다.</summary>
    public XinjeSeries Series { get; }

    /// <summary>확장 주소 영역(표준 16비트 오프셋 초과 또는 정전유지/플래시 확장 디바이스) 여부입니다.</summary>
    public bool IsExtended =>
        Number >= ExtendedAddressThreshold ||
        Device is "ET" or "HM" or "HS" or "HT" or "HC" or "HSC" or "SEM"
            or "ETD" or "HD" or "HSD" or "HTD" or "HCD" or "HSCD" or "FD" or "SFD" or "FS" or "FSD";

    /// <summary>확장 Modbus 24비트(3바이트 BE) 주소 바이트를 반환합니다.</summary>
    public byte[] Get24BitAddressBytes()
    {
        return
        [
            (byte)((Number >> HighByteShift) & ByteMask),
            (byte)((Number >> MidByteShift) & ByteMask),
            (byte)(Number & ByteMask)
        ];
    }

    /// <summary>표준 Modbus PDU 16비트 주소를 계산합니다. (사양서 Part B)</summary>
    public ushort ToModbusPduAddress()
    {
        return Series == XinjeSeries.XC ? CalculateXcModbusAddress() : CalculateXdModbusAddress();
    }

    private ushort CalculateXcModbusAddress()
    {
        if (IsBit)
        {
            return Device switch
            {
                "X" => (ushort)(0x4000 + Number),
                "Y" => (ushort)(0x4800 + Number),
                "S" => (ushort)(0x5000 + Number),
                "M" => (ushort)(Number < XcHighAddressBoundary ? Number : 0x6000 + (Number - XcHighAddressBoundary)),
                "T" => (ushort)(0x6400 + Number),
                "C" => (ushort)(0x6C00 + Number),
                _ => (ushort)Number,
            };
        }

        return Device switch
        {
            "D" => (ushort)(Number < XcHighAddressBoundary ? Number : 0x4000 + (Number - XcHighAddressBoundary)),
            "T" or "TD" => (ushort)(0x3000 + Number),
            "C" or "CD" => (ushort)(0x3800 + Number),
            "F" or "FD" => (ushort)(Number < XcHighAddressBoundary ? 0x4800 + Number : 0x6800 + (Number - XcHighAddressBoundary)),
            "E" or "ED" => (ushort)(0x7000 + Number),
            _ => (ushort)Number,
        };
    }

    private ushort CalculateXdModbusAddress()
    {
        if (IsBit)
        {
            return Device switch
            {
                "X" => CalculateXdIoBitAddress(0x5000, 0x5100, 0x58D0, 0x5BF0),
                "Y" => CalculateXdIoBitAddress(0x6000, 0x6100, 0x68D0, 0x6BF0),
                "M" => (ushort)(0x0000 + Number),
                "S" => (ushort)(0x7000 + Number),
                "SM" => (ushort)(0x9000 + Number),
                "T" => (ushort)(0xA000 + Number),
                "C" => (ushort)(0xB000 + Number),
                "ET" => (ushort)(0xC000 + Number),
                "SEM" => (ushort)(0xC080 + Number),
                "HM" => (ushort)(0xC100 + Number),
                "HS" => (ushort)(0xD900 + Number),
                "HT" => (ushort)(0xE100 + Number),
                "HC" => (ushort)(0xE500 + Number),
                "HSC" => (ushort)(0xE900 + Number),
                _ => (ushort)Number,
            };
        }

        return Device switch
        {
            "ID" => CalculateXdIoWordAddress(0x5000, 0x5100, 0x58D0, 0x5BF0),
            "QD" => CalculateXdIoWordAddress(0x6000, 0x6100, 0x68D0, 0x6BF0),
            "D" => (ushort)(0x0000 + Number),
            "SD" => (ushort)(0x7000 + Number),
            "TD" => (ushort)(0x8000 + Number),
            "CD" => (ushort)(0x9000 + Number),
            "ETD" => (ushort)(0xA000 + Number),
            "HD" => (ushort)(0xA080 + Number),
            "HSD" => (ushort)(0xB880 + Number),
            "HTD" => (ushort)(0xBC80 + Number),
            "HCD" => (ushort)(0xC080 + Number),
            "HSCD" => (ushort)(0xC480 + Number),
            "FD" => (ushort)(0xC4C0 + Number),
            "SFD" => (ushort)(0xE4C0 + Number),
            "FS" or "FSD" => (ushort)(0xF4C0 + Number),
            _ => (ushort)Number,
        };
    }

    private ushort CalculateXdIoBitAddress(ushort baseMain, ushort baseExt, ushort baseBd, ushort baseEd)
    {
        if (Number < XdBitIoBlock1Boundary)
        {
            return (ushort)(baseMain + Number);
        }

        if (Number < XdBitIoBlock2Boundary)
        {
            return (ushort)(baseExt + (Number - XdBitIoBlock1Boundary));
        }

        if (Number < XdBitIoBlock3Boundary)
        {
            return (ushort)(baseBd + (Number - XdBitIoBlock2Boundary));
        }

        return (ushort)(baseEd + (Number - XdBitIoBlock3Boundary));
    }

    private ushort CalculateXdIoWordAddress(ushort baseMain, ushort baseExt, ushort baseBd, ushort baseEd)
    {
        if (Number < XdWordIoBlock1Boundary)
        {
            return (ushort)(baseMain + Number);
        }

        if (Number < XdWordIoBlock2Boundary)
        {
            return (ushort)(baseExt + (Number - XdWordIoBlock1Boundary));
        }

        if (Number < XdWordIoBlock3Boundary)
        {
            return (ushort)(baseBd + (Number - XdWordIoBlock2Boundary));
        }

        return (ushort)(baseEd + (Number - XdWordIoBlock3Boundary));
    }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static XinjeAddress Parse(string address, XinjeSeries series = XinjeSeries.XdXl, bool isBitAccess = false)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!TryParse(address, series, isBitAccess, out XinjeAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        XinjeSeries series,
        bool isBitAccess,
        [NotNullWhen(true)] out XinjeAddress? result,
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
            errorMessage = $"지원하지 않는 XINJE 디바이스 접두사입니다: {address}";
            return false;
        }

        int number;
        if (devicePrefix is "X" or "Y")
        {
            if (!TryParseOctal24Bit(numberText, out number))
            {
                errorMessage = $"유효하지 않은 8진수 XINJE 주소입니다: {address}";
                return false;
            }
        }
        else if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            errorMessage = $"유효하지 않은 10진수 XINJE 주소입니다: {address}";
            return false;
        }

        if (number < 0 || number > Max24BitAddressNumber)
        {
            errorMessage = $"XINJE 주소 번호가 24비트 허용 범위를 벗어났습니다: {address}";
            return false;
        }

        return TryResolveExtendedDevice(trimmed, devicePrefix, number, series, isBitAccess, out result, out errorMessage);
    }

    private static bool TryParseOctal24Bit(string text, out int number)
    {
        number = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        long accumulated = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c < MinOctalDigit || c > MaxOctalDigit)
            {
                return false;
            }

            accumulated = (accumulated * OctalRadix) + (c - MinOctalDigit);
            if (accumulated > Max24BitAddressNumber)
            {
                return false;
            }
        }

        number = (int)accumulated;
        return true;
    }

    private static bool TryExtractDevice(string trimmed, out string devicePrefix, out string numberText)
    {
        devicePrefix = string.Empty;
        numberText = string.Empty;

        foreach (string prefix in OrderedDevicePrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                devicePrefix = prefix;
                numberText = trimmed.Substring(prefix.Length);
                return numberText.Length > 0;
            }
        }

        return false;
    }

    private static bool TryResolveExtendedDevice(
        string original,
        string devicePrefix,
        int number,
        XinjeSeries series,
        bool isBitAccess,
        [NotNullWhen(true)] out XinjeAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        byte code;
        bool isBit;

        switch (devicePrefix)
        {
            case "X": code = 0x01; isBit = true; break;
            case "Y": code = 0x02; isBit = true; break;
            case "M": code = 0x03; isBit = true; break;
            case "S": code = 0x04; isBit = true; break;
            case "T": code = 0x05; isBit = series != XinjeSeries.XC || isBitAccess; break;
            case "C": code = 0x06; isBit = series != XinjeSeries.XC || isBitAccess; break;
            case "ET": code = 0x07; isBit = true; break;
            case "HM": code = 0x08; isBit = true; break;
            case "HS": code = 0x09; isBit = true; break;
            case "HT": code = 0x0A; isBit = true; break;
            case "HC": code = 0x0B; isBit = true; break;
            case "HSC": code = 0x0C; isBit = true; break;
            case "SM": code = 0x0D; isBit = true; break;
            case "SEM": code = 0x03; isBit = true; break;

            case "D": code = 0x80; isBit = false; break;
            case "TD": code = 0x81; isBit = false; break;
            case "CD": code = 0x82; isBit = false; break;
            case "SD": code = 0x83; isBit = false; break;
            case "ETD": code = 0x85; isBit = false; break;
            case "ID": code = 0x86; isBit = false; break;
            case "QD": code = 0x87; isBit = false; break;
            case "HD": code = 0x88; isBit = false; break;
            case "HTD": code = 0x89; isBit = false; break;
            case "HCD": code = 0x8A; isBit = false; break;
            case "HSCD": code = 0x8B; isBit = false; break;
            case "HSD": code = 0x8C; isBit = false; break;
            case "FD": case "F": code = 0x8D; isBit = false; break;
            case "SFD": code = 0x8E; isBit = false; break;
            case "FS": case "FSD": code = 0x8E; isBit = false; break;
            case "E": case "ED": code = 0x80; isBit = false; break;
            default:
                errorMessage = $"알 수 없는 XINJE 디바이스입니다: {devicePrefix}";
                return false;
        }

        result = new XinjeAddress(original, devicePrefix, number, code, isBit, series);
        return true;
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="XinjeAddress"/>를 반환합니다.</summary>
    public XinjeAddress Offset(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        return new XinjeAddress($"{Original}+{count}", Device, checked(Number + count), ExtendedDeviceCode, IsBit, Series);
    }
}
