using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Plc.Protocols.Vigor;

/// <summary>VIGOR VS 계열 PLC 디바이스 주소 표현입니다. (사양서 §1.5, §1.6)</summary>
public sealed class VigorAddress
{
    private const int MaxAddressNumber = 999999;
    private const int MaxOctalAddressNumber = 777;
    private const int MaxOctalValue = 511; // 8진수 777의 실제 10진 값
    private const int MaxStandardRegisterNumber = 8999;
    private const int MaxStepRelayNumber = 4095;
    private const int MaxSpecialDeviceNumber = 255;
    private const int MaxTimerCounterIndex = 255;
    private const int MaxFileRegisterNumber = 23999;
    private const int Max16BitCounterNumber = 199;
    private const int Counter32BitStartNumber = 200;
    private const int SpecialAreaStartNumber = 9000;

    private const int TwoCharPrefixLength = 2;
    private const int SingleCharPrefixLength = 1;
    private const int DecimalBase = 10;
    private const int DecimalStep10 = 10;
    private const int DecimalStep100 = 100;
    private const int DecimalStep1000 = 1000;
    private const int DecimalStep10000 = 10000;
    private const int DecimalStep100000 = 100000;
    private const int OctalRadix = 8;
    private const int BcdHighNibbleShift = 4;

    private const byte DeviceCodeInputX = 0x90;
    private const byte DeviceCodeOutputY = 0x91;
    private const byte DeviceCodeAuxiliaryRelayM = 0x92;
    private const byte DeviceCodeStepRelayS = 0x93;
    private const byte DeviceCodeSpecialRelaySm = 0x94;
    private const byte DeviceCodeTimerCoilTc = 0x98;
    private const byte DeviceCodeTimerContactTs = 0x99;
    private const byte DeviceCodeCounterCoilCc = 0x9C;
    private const byte DeviceCodeCounterContactCs = 0x9D;
    private const byte DeviceCodeDataRegisterD = 0xA0;
    private const byte DeviceCodeSpecialRegisterSd = 0xA1;
    private const byte DeviceCodeFileRegisterR = 0xA2;
    private const byte DeviceCodeTimerPresentValueT = 0xA8;
    private const byte DeviceCodeCounter16BitPresentValueC = 0xAC;
    private const byte DeviceCodeCounter32BitPresentValueC = 0xAD;

    private static readonly string[] TwoCharPrefixes = ["SM", "SD", "TC", "TS", "CC", "CS"];
    private readonly byte[] _bcdAddress;

    private VigorAddress(
        string original,
        string device,
        int number,
        byte deviceCode,
        byte[] bcdAddress,
        bool isBit,
        bool is32BitWord)
    {
        Original = original;
        Device = device;
        Number = number;
        DeviceCode = deviceCode;
        _bcdAddress = bcdAddress;
        IsBit = isBit;
        Is32BitWord = is32BitWord;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string Original { get; }

    /// <summary>디바이스 접두사입니다. (예: "D", "M", "SM", "SD", "R", "C")</summary>
    public string Device { get; }

    /// <summary>변환된 숫자 번호입니다. (M9000의 경우 0)</summary>
    public int Number { get; }

    /// <summary>VIGOR 프로토콜 1바이트 디바이스 코드입니다.</summary>
    public byte DeviceCode { get; }

    /// <summary>리틀엔디언 3바이트 BCD 주소 복사본입니다.</summary>
    public byte[] BcdAddress => (byte[])_bcdAddress.Clone();

    internal void WriteBcdAddress(byte[] destination, int offset)
    {
        Buffer.BlockCopy(_bcdAddress, 0, destination, offset, _bcdAddress.Length);
    }

    /// <summary>비트 접점 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>32비트 카운터 레지스터(C200 이상) 여부입니다.</summary>
    public bool Is32BitWord { get; }

    /// <summary>주소 문자열을 파싱합니다.</summary>
    public static VigorAddress Parse(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!TryParse(address, out VigorAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 파싱을 시도합니다.</summary>
    public static bool TryParse(
        string? address,
        [NotNullWhen(true)] out VigorAddress? result,
        [NotNullWhen(false)] out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            errorMessage = "주소 문자열은 필수입니다.";
            return false;
        }

        string trimmed = address.Trim().ToUpperInvariant();
        if (!TryExtractDevice(trimmed, out string devicePrefix, out string numberText))
        {
            errorMessage = $"지원하지 않는 VIGOR 디바이스입니다: {address}";
            return false;
        }

        if (IsOctalDevice(devicePrefix) && !IsValidOctalDigits(numberText))
        {
            errorMessage = $"X/Y 디바이스 주소는 8진수(0~7) 자릿수만 사용할 수 있습니다: {address}";
            return false;
        }

        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            || number < 0
            || number > MaxAddressNumber
            || (IsOctalDevice(devicePrefix) && number > MaxOctalAddressNumber))
        {
            errorMessage = $"VIGOR 주소 번호가 범위를 초과했거나 유효하지 않습니다: {address}";
            return false;
        }

        return TryResolveDevice(trimmed, devicePrefix, number, out result, out errorMessage);
    }

    /// <summary>10진수 번호를 6자리 3바이트 BCD 리틀엔디언으로 인코딩합니다. (§1.6)</summary>
    public static byte[] EncodeBcdLe(int number)
    {
        if (number < 0 || number > MaxAddressNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, $"BCD 주소 번호는 0~{MaxAddressNumber} 범위여야 합니다.");
        }

        int digit0 = number % DecimalBase;
        int digit1 = (number / DecimalStep10) % DecimalBase;
        int digit2 = (number / DecimalStep100) % DecimalBase;
        int digit3 = (number / DecimalStep1000) % DecimalBase;
        int digit4 = (number / DecimalStep10000) % DecimalBase;
        int digit5 = (number / DecimalStep100000) % DecimalBase;

        byte lowByte = (byte)((digit1 << BcdHighNibbleShift) | digit0);
        byte middleByte = (byte)((digit3 << BcdHighNibbleShift) | digit2);
        byte highByte = (byte)((digit5 << BcdHighNibbleShift) | digit4);

        return [lowByte, middleByte, highByte];
    }

    /// <summary>오프셋만큼 주소를 이동한 새 <see cref="VigorAddress"/>를 반환합니다.</summary>
    public VigorAddress Offset(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "오프셋은 0 이상이어야 합니다.");
        }

        int nextNumber;
        if (IsOctalDevice(Device))
        {
            int currentOctalValue = OctalDecimalToValue(Number);
            long nextOctalValueLong = (long)currentOctalValue + count;
            if (nextOctalValueLong > MaxOctalValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    count,
                    $"8진수 디바이스({Device}) 오프셋 결과가 최대 범위(0~{MaxOctalAddressNumber})를 초과합니다.");
            }

            nextNumber = ValueToOctalDecimal((int)nextOctalValueLong);
        }
        else
        {
            long nextNumberLong = (long)Number + count;
            if (nextNumberLong > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "주소 오프셋이 허용 범위를 초과합니다.");
            }

            nextNumber = (int)nextNumberLong;
        }

        if (!IsWithinDeviceCodeBounds(DeviceCode, nextNumber))
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"오프셋 결과 주소 번호({nextNumber})가 디바이스(0x{DeviceCode:X2}) 유효 범위를 벗어났습니다.");
        }

        byte[] nextBcdAddress = EncodeBcdLe(nextNumber);
        return new VigorAddress($"{Original}+{count}", Device, nextNumber, DeviceCode, nextBcdAddress, IsBit, Is32BitWord);
    }

    private static bool TryExtractDevice(string trimmed, out string devicePrefix, out string numberText)
    {
        foreach (string prefixCandidate in TwoCharPrefixes)
        {
            if (trimmed.StartsWith(prefixCandidate, StringComparison.Ordinal))
            {
                devicePrefix = prefixCandidate;
                numberText = trimmed.Substring(TwoCharPrefixLength);
                return numberText.Length > 0;
            }
        }

        char firstChar = trimmed[0];
        devicePrefix = firstChar.ToString();
        numberText = trimmed.Substring(SingleCharPrefixLength);
        return firstChar is 'X' or 'Y' or 'M' or 'S' or 'D' or 'R' or 'T' or 'C' && numberText.Length > 0;
    }

    private static bool TryResolveDevice(
        string original,
        string devicePrefix,
        int number,
        [NotNullWhen(true)] out VigorAddress? result,
        out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (TryResolveBitDevice(devicePrefix, ref number, out byte bitDeviceCode))
        {
            if (!IsWithinDeviceCodeBounds(bitDeviceCode, number))
            {
                errorMessage = $"VIGOR 비트 주소 번호가 디바이스(0x{bitDeviceCode:X2}) 범위를 초과했습니다: {original}";
                return false;
            }

            byte[] bitBcdAddress = EncodeBcdLe(number);
            result = new VigorAddress(original, devicePrefix, number, bitDeviceCode, bitBcdAddress, isBit: true, is32BitWord: false);
            return true;
        }

        if (TryResolveWordDevice(devicePrefix, ref number, out byte wordDeviceCode, out bool is32BitWord))
        {
            if (!IsWithinDeviceCodeBounds(wordDeviceCode, number))
            {
                errorMessage = $"VIGOR 워드 주소 번호가 디바이스(0x{wordDeviceCode:X2}) 범위를 초과했습니다: {original}";
                return false;
            }

            byte[] wordBcdAddress = EncodeBcdLe(number);
            result = new VigorAddress(original, devicePrefix, number, wordDeviceCode, wordBcdAddress, isBit: false, is32BitWord);
            return true;
        }

        errorMessage = $"알 수 없는 디바이스 접두사입니다: {devicePrefix}";
        return false;
    }

    private static bool TryResolveBitDevice(string devicePrefix, ref int number, out byte deviceCode)
    {
        switch (devicePrefix)
        {
            case "X":
                deviceCode = DeviceCodeInputX;
                return true;
            case "Y":
                deviceCode = DeviceCodeOutputY;
                return true;
            case "M":
                if (number >= SpecialAreaStartNumber)
                {
                    deviceCode = DeviceCodeSpecialRelaySm;
                    number -= SpecialAreaStartNumber;
                }
                else
                {
                    deviceCode = DeviceCodeAuxiliaryRelayM;
                }

                return true;
            case "S":
                deviceCode = DeviceCodeStepRelayS;
                return true;
            case "SM":
                deviceCode = DeviceCodeSpecialRelaySm;
                return true;
            case "TC":
                deviceCode = DeviceCodeTimerCoilTc;
                return true;
            case "TS":
                deviceCode = DeviceCodeTimerContactTs;
                return true;
            case "CC":
                deviceCode = DeviceCodeCounterCoilCc;
                return true;
            case "CS":
                deviceCode = DeviceCodeCounterContactCs;
                return true;
            default:
                deviceCode = 0;
                return false;
        }
    }

    private static bool TryResolveWordDevice(string devicePrefix, ref int number, out byte deviceCode, out bool is32BitWord)
    {
        is32BitWord = false;
        switch (devicePrefix)
        {
            case "D":
                if (number >= SpecialAreaStartNumber)
                {
                    deviceCode = DeviceCodeSpecialRegisterSd;
                    number -= SpecialAreaStartNumber;
                }
                else
                {
                    deviceCode = DeviceCodeDataRegisterD;
                }

                return true;
            case "SD":
                deviceCode = DeviceCodeSpecialRegisterSd;
                return true;
            case "R":
                deviceCode = DeviceCodeFileRegisterR;
                return true;
            case "T":
                deviceCode = DeviceCodeTimerPresentValueT;
                return true;
            case "C":
                if (number >= Counter32BitStartNumber)
                {
                    deviceCode = DeviceCodeCounter32BitPresentValueC;
                    is32BitWord = true;
                }
                else
                {
                    deviceCode = DeviceCodeCounter16BitPresentValueC;
                }

                return true;
            default:
                deviceCode = 0;
                return false;
        }
    }

    private static bool IsWithinDeviceCodeBounds(byte deviceCode, int number) => deviceCode switch
    {
        DeviceCodeInputX or DeviceCodeOutputY => number is >= 0 and <= MaxOctalAddressNumber,
        DeviceCodeAuxiliaryRelayM or DeviceCodeDataRegisterD => number is >= 0 and <= MaxStandardRegisterNumber,
        DeviceCodeStepRelayS => number is >= 0 and <= MaxStepRelayNumber,
        DeviceCodeSpecialRelaySm or DeviceCodeSpecialRegisterSd => number is >= 0 and <= MaxSpecialDeviceNumber,
        DeviceCodeTimerCoilTc or DeviceCodeTimerContactTs or DeviceCodeCounterCoilCc or DeviceCodeCounterContactCs or DeviceCodeTimerPresentValueT => number is >= 0 and <= MaxTimerCounterIndex,
        DeviceCodeFileRegisterR => number is >= 0 and <= MaxFileRegisterNumber,
        DeviceCodeCounter16BitPresentValueC => number is >= 0 and <= Max16BitCounterNumber,
        DeviceCodeCounter32BitPresentValueC => number is >= Counter32BitStartNumber and <= MaxTimerCounterIndex,
        _ => false,
    };

    private static bool IsOctalDevice(string devicePrefix)
    {
        return devicePrefix is "X" or "Y";
    }

    private static bool IsValidOctalDigits(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is < '0' or > '7')
            {
                return false;
            }
        }

        return true;
    }

    private static int OctalDecimalToValue(int octalDecimal)
    {
        int value = 0;
        int multiplier = 1;
        int remaining = octalDecimal;

        while (remaining > 0)
        {
            int digit = remaining % DecimalBase;
            value = checked(value + (digit * multiplier));
            remaining /= DecimalBase;
            if (remaining > 0)
            {
                multiplier = checked(multiplier * OctalRadix);
            }
        }

        return value;
    }

    private static int ValueToOctalDecimal(int value)
    {
        int octalDecimal = 0;
        int multiplier = 1;
        int remaining = value;

        while (remaining > 0)
        {
            int digit = remaining % OctalRadix;
            octalDecimal = checked(octalDecimal + (digit * multiplier));
            remaining /= OctalRadix;
            if (remaining > 0)
            {
                multiplier = checked(multiplier * DecimalBase);
            }
        }

        return octalDecimal;
    }
}
