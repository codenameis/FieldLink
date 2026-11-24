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
    private const int MaxStandardXdDmIndex = 20479;
    private const int Max24BitAddressNumber = 0xFFFFFF;
    private const char MinOctalDigit = '0';
    private const char MaxOctalDigit = '7';

    private const int XcIoMaxOctal = 0x021F; // X0~X1037 (8진)
    private const int XcSMaxIndex = 1023;
    private const int XcHighAddressBoundary = 8000;
    private const int XcDMMaxIndex = 8511;
    private const int XcTimerMaxIndex = 618;
    private const int XcCounterMaxIndex = 634;
    private const int XcFLowMaxIndex = 5000;
    private const int XcFHighMaxIndex = 8511;
    private const int XcEMaxIndex = 36863;

    private const ushort XcXBasePdu = 0x4000;
    private const ushort XcYBasePdu = 0x4800;
    private const ushort XcSBasePdu = 0x5000;
    private const ushort XcMHighBasePdu = 0x6000;
    private const ushort XcTBitBasePdu = 0x6400;
    private const ushort XcCBitBasePdu = 0x6C00;
    private const ushort XcTWordBasePdu = 0x3000;
    private const ushort XcCWordBasePdu = 0x3800;
    private const ushort XcDHighBasePdu = 0x4000;
    private const ushort XcFLowBasePdu = 0x4800;
    private const ushort XcFHighBasePdu = 0x6800;
    private const ushort XcEBasePdu = 0x7000;

    private const int XdBitIoBlock1Boundary = 0x1000;
    private const int XdBitIoBlock2Boundary = 0x2000;
    private const int XdBitIoBlock3Boundary = 0x3000;
    private const int XdBitIoMainMax = 0x003F; // X0~X77
    private const int XdBitIoExtMax = 0x13FF;  // X10000~X11777
    private const int XdBitIoBdMax = 0x207F;   // X20000~X20177
    private const int XdBitIoEdMax = 0x303F;   // X30000~X30077

    private const int XdWordIoBlock1Boundary = 10000;
    private const int XdWordIoBlock2Boundary = 20000;
    private const int XdWordIoBlock3Boundary = 30000;
    private const int XdWordIoMainMax = 99;
    private const int XdWordIoExtMax = 10999;
    private const int XdWordIoBdMax = 20099;
    private const int XdWordIoEdMax = 30099;

    private const ushort XdXIoMainBasePdu = 0x5000;
    private const ushort XdXIoExtBasePdu = 0x5100;
    private const ushort XdXIoBdBasePdu = 0x58D0;
    private const ushort XdXIoEdBasePdu = 0x5BF0;
    private const ushort XdYIoMainBasePdu = 0x6000;
    private const ushort XdYIoExtBasePdu = 0x6100;
    private const ushort XdYIoBdBasePdu = 0x68D0;
    private const ushort XdYIoEdBasePdu = 0x6BF0;

    private const ushort XdSBasePdu = 0x7000;
    private const ushort XdSmBasePdu = 0x9000;
    private const ushort XdTBitBasePdu = 0xA000;
    private const ushort XdCBitBasePdu = 0xB000;
    private const ushort XdEtBitBasePdu = 0xC000;
    private const ushort XdSemBitBasePdu = 0xC080;
    private const ushort XdHmBitBasePdu = 0xC100;
    private const ushort XdHsBitBasePdu = 0xD900;
    private const ushort XdHtBitBasePdu = 0xE100;
    private const ushort XdHcBitBasePdu = 0xE500;
    private const ushort XdHscBitBasePdu = 0xE900;

    private const ushort XdSdBasePdu = 0x7000;
    private const ushort XdTdBasePdu = 0x8000;
    private const ushort XdCdBasePdu = 0x9000;
    private const ushort XdEtdBasePdu = 0xA000;
    private const ushort XdHdBasePdu = 0xA080;
    private const ushort XdHsdBasePdu = 0xB880;
    private const ushort XdHtdBasePdu = 0xBC80;
    private const ushort XdHcdBasePdu = 0xC080;
    private const ushort XdHscdBasePdu = 0xC480;
    private const ushort XdFdBasePdu = 0xC4C0;
    private const ushort XdSfdBasePdu = 0xE4C0;
    private const ushort XdFsBasePdu = 0xF4C0;

    private const int XdSMaxIndex = 7999;
    private const int XdSmSdTdCdSfdMaxIndex = 4095;
    private const int XdEtHscMaxIndex = 39;
    private const int XdSemMaxIndex = 127;
    private const int XdHmHdMaxIndex = 6143;
    private const int XdHsMaxIndex = 999;
    private const int XdHtHcHsdMaxIndex = 1023;
    private const int XdFdMaxIndex = 8191;
    private const int XdFsMaxIndex = 47;

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
        byte? extendedDeviceCode,
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

    /// <summary>확장 Modbus 1바이트 디바이스 코드입니다. 확장 미지원 디바이스(SEM, FS/FSD, E/ED 등)는 null입니다. (§A.1.4)</summary>
    public byte? ExtendedDeviceCode { get; }

    /// <summary>비트 영역 또는 비트 접근 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>PLC 시리즈입니다.</summary>
    public XinjeSeries Series { get; }

    /// <summary>확장 주소 영역(표준 16비트 오프셋 초과 또는 정전유지/플래시 확장 디바이스) 여부입니다.</summary>
    public bool IsExtended =>
        ExtendedDeviceCode.HasValue &&
        (Number >= ExtendedAddressThreshold ||
         Device is "ET" or "HM" or "HS" or "HT" or "HC" or "HSC"
             or "ETD" or "HD" or "HSD" or "HTD" or "HCD" or "HSCD" or "FD" or "SFD");

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static XinjeAddress Parse(string address, XinjeSeries series = XinjeSeries.XdXl, bool isBitAccess = false)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!Enum.IsDefined(typeof(XinjeSeries), series))
        {
            throw new ArgumentOutOfRangeException(nameof(series), series, "지원하지 않는 XINJE 시리즈입니다.");
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

        if (!Enum.IsDefined(typeof(XinjeSeries), series))
        {
            errorMessage = $"지원하지 않는 XINJE 시리즈입니다: {series}";
            return false;
        }

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

        if (!TryParseDeviceNumber(devicePrefix, numberText, address, out int number, out errorMessage))
        {
            return false;
        }

        return TryResolveDevice(trimmed, devicePrefix, number, series, isBitAccess, out result, out errorMessage);
    }

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

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="XinjeAddress"/>를 반환합니다.</summary>
    public XinjeAddress Offset(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        long nextNumberLong = (long)Number + count;
        if (nextNumberLong > Max24BitAddressNumber || !IsNumberInSeriesDeviceBounds(Series, Device, (int)nextNumberLong))
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, $"이동된 주소가 {Series} {Device} 허용 범위를 초과했습니다: {Original}+{count}");
        }

        return new XinjeAddress($"{Original}+{count}", Device, (int)nextNumberLong, ExtendedDeviceCode, IsBit, Series);
    }

    internal void ValidateContiguousRange(long unitCount, bool isStandardModbus, string paramName)
    {
        if (unitCount <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, unitCount, "요청 수량은 1 이상이어야 합니다.");
        }

        long endNumber = (long)Number + unitCount - 1;
        if (endNumber > Max24BitAddressNumber)
        {
            throw new ArgumentOutOfRangeException(paramName, unitCount, $"요청 범위가 24비트 최대 주소({Max24BitAddressNumber})를 초과합니다.");
        }

        if (!IsRangeValidForDevice(Series, Device, Number, (int)endNumber, isStandardModbus))
        {
            throw new ArgumentOutOfRangeException(paramName, unitCount, $"요청 범위가 {Series} {Device} 디바이스 허용 범위를 초과합니다.");
        }
    }

    private ushort CalculateXcModbusAddress()
    {
        if (IsBit)
        {
            return Device switch
            {
                "X" => checked((ushort)(XcXBasePdu + Number)),
                "Y" => checked((ushort)(XcYBasePdu + Number)),
                "S" => checked((ushort)(XcSBasePdu + Number)),
                "M" => checked((ushort)(Number < XcHighAddressBoundary ? Number : XcMHighBasePdu + (Number - XcHighAddressBoundary))),
                "T" => checked((ushort)(XcTBitBasePdu + Number)),
                "C" => checked((ushort)(XcCBitBasePdu + Number)),
                _ => throw new InvalidOperationException($"XC 시리즈 비트 표준 Modbus 매핑을 지원하지 않는 디바이스입니다: {Device}"),
            };
        }

        return Device switch
        {
            "D" => checked((ushort)(Number < XcHighAddressBoundary ? Number : XcDHighBasePdu + (Number - XcHighAddressBoundary))),
            "T" or "TD" => checked((ushort)(XcTWordBasePdu + Number)),
            "C" or "CD" => checked((ushort)(XcCWordBasePdu + Number)),
            "F" or "FD" => CalculateXcFlashModbusAddress(),
            "E" or "ED" => checked((ushort)(XcEBasePdu + Number)),
            _ => throw new InvalidOperationException($"XC 시리즈 워드 표준 Modbus 매핑을 지원하지 않는 디바이스입니다: {Device}"),
        };
    }

    private ushort CalculateXcFlashModbusAddress()
    {
        if (Number <= XcFLowMaxIndex)
        {
            return checked((ushort)(XcFLowBasePdu + Number));
        }

        if (Number >= XcHighAddressBoundary && Number <= XcFHighMaxIndex)
        {
            return checked((ushort)(XcFHighBasePdu + (Number - XcHighAddressBoundary)));
        }

        throw new InvalidOperationException($"XC 시리즈 F/FD 표준 Modbus 범위(0~{XcFLowMaxIndex}, {XcHighAddressBoundary}~{XcFHighMaxIndex})를 벗어났습니다: {Original}");
    }

    private ushort CalculateXdModbusAddress()
    {
        if (IsBit)
        {
            return Device switch
            {
                "X" => CalculateXdIoBitAddress(XdXIoMainBasePdu, XdXIoExtBasePdu, XdXIoBdBasePdu, XdXIoEdBasePdu),
                "Y" => CalculateXdIoBitAddress(XdYIoMainBasePdu, XdYIoExtBasePdu, XdYIoBdBasePdu, XdYIoEdBasePdu),
                "M" when Number <= MaxStandardXdDmIndex => (ushort)Number,
                "S" => checked((ushort)(XdSBasePdu + Number)),
                "SM" => checked((ushort)(XdSmBasePdu + Number)),
                "T" => checked((ushort)(XdTBitBasePdu + Number)),
                "C" => checked((ushort)(XdCBitBasePdu + Number)),
                "ET" => checked((ushort)(XdEtBitBasePdu + Number)),
                "SEM" => checked((ushort)(XdSemBitBasePdu + Number)),
                "HM" => checked((ushort)(XdHmBitBasePdu + Number)),
                "HS" => checked((ushort)(XdHsBitBasePdu + Number)),
                "HT" => checked((ushort)(XdHtBitBasePdu + Number)),
                "HC" => checked((ushort)(XdHcBitBasePdu + Number)),
                "HSC" => checked((ushort)(XdHscBitBasePdu + Number)),
                _ => throw new InvalidOperationException($"XD/XL 시리즈 비트 표준 Modbus 매핑을 지원하지 않는 디바이스입니다: {Device} (주소: {Original})"),
            };
        }

        return Device switch
        {
            "ID" => CalculateXdIoWordAddress(XdXIoMainBasePdu, XdXIoExtBasePdu, XdXIoBdBasePdu, XdXIoEdBasePdu),
            "QD" => CalculateXdIoWordAddress(XdYIoMainBasePdu, XdYIoExtBasePdu, XdYIoBdBasePdu, XdYIoEdBasePdu),
            "D" when Number <= MaxStandardXdDmIndex => (ushort)Number,
            "SD" => checked((ushort)(XdSdBasePdu + Number)),
            "TD" => checked((ushort)(XdTdBasePdu + Number)),
            "CD" => checked((ushort)(XdCdBasePdu + Number)),
            "ETD" => checked((ushort)(XdEtdBasePdu + Number)),
            "HD" => checked((ushort)(XdHdBasePdu + Number)),
            "HSD" => checked((ushort)(XdHsdBasePdu + Number)),
            "HTD" => checked((ushort)(XdHtdBasePdu + Number)),
            "HCD" => checked((ushort)(XdHcdBasePdu + Number)),
            "HSCD" => checked((ushort)(XdHscdBasePdu + Number)),
            "FD" => checked((ushort)(XdFdBasePdu + Number)),
            "SFD" => checked((ushort)(XdSfdBasePdu + Number)),
            "FS" or "FSD" => checked((ushort)(XdFsBasePdu + Number)),
            _ => throw new InvalidOperationException($"XD/XL 시리즈 워드 표준 Modbus 매핑을 지원하지 않는 디바이스입니다: {Device} (주소: {Original})"),
        };
    }

    private ushort CalculateXdIoBitAddress(ushort baseMain, ushort baseExtension, ushort baseBoard, ushort baseExtensionBoard)
    {
        if (Number <= XdBitIoMainMax)
        {
            return (ushort)(baseMain + Number);
        }

        if (Number >= XdBitIoBlock1Boundary && Number <= XdBitIoExtMax)
        {
            return (ushort)(baseExtension + (Number - XdBitIoBlock1Boundary));
        }

        if (Number >= XdBitIoBlock2Boundary && Number <= XdBitIoBdMax)
        {
            return (ushort)(baseBoard + (Number - XdBitIoBlock2Boundary));
        }

        if (Number >= XdBitIoBlock3Boundary && Number <= XdBitIoEdMax)
        {
            return (ushort)(baseExtensionBoard + (Number - XdBitIoBlock3Boundary));
        }

        throw new InvalidOperationException($"XD/XL I/O 비트 주소가 표준 Modbus 매핑 블록 범위를 벗어났습니다: {Original}");
    }

    private ushort CalculateXdIoWordAddress(ushort baseMain, ushort baseExtension, ushort baseBoard, ushort baseExtensionBoard)
    {
        if (Number <= XdWordIoMainMax)
        {
            return (ushort)(baseMain + Number);
        }

        if (Number >= XdWordIoBlock1Boundary && Number <= XdWordIoExtMax)
        {
            return (ushort)(baseExtension + (Number - XdWordIoBlock1Boundary));
        }

        if (Number >= XdWordIoBlock2Boundary && Number <= XdWordIoBdMax)
        {
            return (ushort)(baseBoard + (Number - XdWordIoBlock2Boundary));
        }

        if (Number >= XdWordIoBlock3Boundary && Number <= XdWordIoEdMax)
        {
            return (ushort)(baseExtensionBoard + (Number - XdWordIoBlock3Boundary));
        }

        throw new InvalidOperationException($"XD/XL I/O 워드 주소가 표준 Modbus 매핑 블록 범위를 벗어났습니다: {Original}");
    }

    private static bool TryParseDeviceNumber(
        string devicePrefix,
        string numberText,
        string originalInput,
        out int number,
        out string? errorMessage)
    {
        errorMessage = null;
        if (devicePrefix is "X" or "Y")
        {
            if (!TryParseOctal24Bit(numberText, out number))
            {
                errorMessage = $"유효하지 않은 8진수 XINJE 주소입니다: {originalInput}";
                return false;
            }
        }
        else if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            errorMessage = $"유효하지 않은 10진수 XINJE 주소입니다: {originalInput}";
            return false;
        }

        if (number < 0 || number > Max24BitAddressNumber)
        {
            errorMessage = $"XINJE 주소 번호가 24비트 허용 범위를 벗어났습니다: {originalInput}";
            return false;
        }

        return true;
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
            char digit = text[i];
            if (digit < MinOctalDigit || digit > MaxOctalDigit)
            {
                return false;
            }

            accumulated = (accumulated * OctalRadix) + (digit - MinOctalDigit);
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

    private static bool TryResolveDevice(
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

        if (!IsNumberInSeriesDeviceBounds(series, devicePrefix, number))
        {
            errorMessage = $"{series} 시리즈에서 지원하지 않거나 범위를 초과한 주소입니다: {original}";
            return false;
        }

        bool isBit = ResolveIsBit(devicePrefix, series, isBitAccess);
        byte? extendedCode = ResolveExtendedDeviceCode(devicePrefix);

        result = new XinjeAddress(original, devicePrefix, number, extendedCode, isBit, series);
        return true;
    }

    private static bool ResolveIsBit(string devicePrefix, XinjeSeries series, bool isBitAccess) => devicePrefix switch
    {
        "X" or "Y" or "M" or "S" or "ET" or "HM" or "HS" or "HT" or "HC" or "HSC" or "SM" or "SEM" => true,
        "T" or "C" => series != XinjeSeries.XC || isBitAccess,
        _ => false,
    };

    private static byte? ResolveExtendedDeviceCode(string devicePrefix) => devicePrefix switch
    {
        "X" => (byte)XinjeDeviceCode.X,
        "Y" => (byte)XinjeDeviceCode.Y,
        "M" => (byte)XinjeDeviceCode.M,
        "S" => (byte)XinjeDeviceCode.S,
        "T" => (byte)XinjeDeviceCode.T,
        "C" => (byte)XinjeDeviceCode.C,
        "ET" => (byte)XinjeDeviceCode.ET,
        "HM" => (byte)XinjeDeviceCode.HM,
        "HS" => (byte)XinjeDeviceCode.HS,
        "HT" => (byte)XinjeDeviceCode.HT,
        "HC" => (byte)XinjeDeviceCode.HC,
        "HSC" => (byte)XinjeDeviceCode.HSC,
        "SM" => (byte)XinjeDeviceCode.SM,
        "D" => (byte)XinjeDeviceCode.D,
        "TD" => (byte)XinjeDeviceCode.TD,
        "CD" => (byte)XinjeDeviceCode.CD,
        "SD" => (byte)XinjeDeviceCode.SD,
        "ETD" => (byte)XinjeDeviceCode.ETD,
        "ID" => (byte)XinjeDeviceCode.ID,
        "QD" => (byte)XinjeDeviceCode.QD,
        "HD" => (byte)XinjeDeviceCode.HD,
        "HTD" => (byte)XinjeDeviceCode.HTD,
        "HCD" => (byte)XinjeDeviceCode.HCD,
        "HSCD" => (byte)XinjeDeviceCode.HSCD,
        "HSD" => (byte)XinjeDeviceCode.HSD,
        "FD" => (byte)XinjeDeviceCode.FD,
        "SFD" => (byte)XinjeDeviceCode.SFD,
        _ => null,
    };

    private static bool IsNumberInSeriesDeviceBounds(XinjeSeries series, string devicePrefix, int number)
    {
        if (number < 0)
        {
            return false;
        }

        if (series == XinjeSeries.XC)
        {
            return devicePrefix switch
            {
                "X" or "Y" => number <= XcIoMaxOctal,
                "S" => number <= XcSMaxIndex,
                "M" or "D" => number <= XcDMMaxIndex,
                "T" or "TD" => number <= XcTimerMaxIndex,
                "C" or "CD" => number <= XcCounterMaxIndex,
                "F" or "FD" => number <= XcFLowMaxIndex || (number >= XcHighAddressBoundary && number <= XcFHighMaxIndex),
                "E" or "ED" => number <= XcEMaxIndex,
                _ => false,
            };
        }

        return devicePrefix switch
        {
            "X" or "Y" => IsValidXdIoBitNumber(number),
            "ID" or "QD" => IsValidXdIoWordNumber(number),
            "M" or "D" => number <= Max24BitAddressNumber,
            "S" => number <= XdSMaxIndex,
            "SM" or "SD" or "TD" or "CD" or "SFD" => number <= XdSmSdTdCdSfdMaxIndex,
            "T" or "C" => number <= XdSmSdTdCdSfdMaxIndex,
            "ET" or "ETD" or "HSC" or "HSCD" => number <= XdEtHscMaxIndex,
            "SEM" => number <= XdSemMaxIndex,
            "HM" or "HD" => number <= XdHmHdMaxIndex,
            "HS" => number <= XdHsMaxIndex,
            "HT" or "HC" or "HSD" or "HTD" or "HCD" => number <= XdHtHcHsdMaxIndex,
            "FD" => number <= XdFdMaxIndex,
            "FS" or "FSD" => number <= XdFsMaxIndex,
            _ => false,
        };
    }

    private static bool IsRangeValidForDevice(XinjeSeries series, string devicePrefix, int startNumber, int endNumber, bool isStandardModbus)
    {
        if (!IsNumberInSeriesDeviceBounds(series, devicePrefix, startNumber) ||
            !IsNumberInSeriesDeviceBounds(series, devicePrefix, endNumber))
        {
            return false;
        }

        if (series == XinjeSeries.XC && devicePrefix is "F" or "FD")
        {
            return (startNumber <= XcFLowMaxIndex && endNumber <= XcFLowMaxIndex) ||
                   (startNumber >= XcHighAddressBoundary && endNumber <= XcFHighMaxIndex);
        }

        if (series == XinjeSeries.XdXl)
        {
            if (isStandardModbus && devicePrefix is "M" or "D" && endNumber > MaxStandardXdDmIndex)
            {
                return false;
            }

            if (devicePrefix is "X" or "Y")
            {
                return AreInSameXdIoBitBlock(startNumber, endNumber);
            }

            if (devicePrefix is "ID" or "QD")
            {
                return AreInSameXdIoWordBlock(startNumber, endNumber);
            }
        }

        return true;
    }

    private static bool IsValidXdIoBitNumber(int number) =>
        number <= XdBitIoMainMax ||
        (number >= XdBitIoBlock1Boundary && number <= XdBitIoExtMax) ||
        (number >= XdBitIoBlock2Boundary && number <= XdBitIoBdMax) ||
        (number >= XdBitIoBlock3Boundary && number <= XdBitIoEdMax);

    private static bool AreInSameXdIoBitBlock(int startNumber, int endNumber) =>
        (startNumber <= XdBitIoMainMax && endNumber <= XdBitIoMainMax) ||
        (startNumber >= XdBitIoBlock1Boundary && endNumber <= XdBitIoExtMax) ||
        (startNumber >= XdBitIoBlock2Boundary && endNumber <= XdBitIoBdMax) ||
        (startNumber >= XdBitIoBlock3Boundary && endNumber <= XdBitIoEdMax);

    private static bool IsValidXdIoWordNumber(int number) =>
        number <= XdWordIoMainMax ||
        (number >= XdWordIoBlock1Boundary && number <= XdWordIoExtMax) ||
        (number >= XdWordIoBlock2Boundary && number <= XdWordIoBdMax) ||
        (number >= XdWordIoBlock3Boundary && number <= XdWordIoEdMax);

    private static bool AreInSameXdIoWordBlock(int startNumber, int endNumber) =>
        (startNumber <= XdWordIoMainMax && endNumber <= XdWordIoMainMax) ||
        (startNumber >= XdWordIoBlock1Boundary && endNumber <= XdWordIoExtMax) ||
        (startNumber >= XdWordIoBlock2Boundary && endNumber <= XdWordIoBdMax) ||
        (startNumber >= XdWordIoBlock3Boundary && endNumber <= XdWordIoEdMax);
}
