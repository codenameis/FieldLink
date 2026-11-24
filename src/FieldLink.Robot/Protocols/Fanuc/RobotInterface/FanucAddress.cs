using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC Robot Interface의 1-based 메모리 주소와 셀렉터를 나타내는 불변 주소 객체입니다.</summary>
public sealed class FanucAddress
{
    private const ushort SdoSplitThreshold = 11001;
    private const ushort SdoHighOffset = 11000;
    private const ushort GoSplitThreshold = 10001;
    private const ushort GoHighOffset = 6000;
    private const ushort RdiRdoOffset = 5000;
    private const ushort UiUoOffset = 6000;
    private const ushort SiSoOffset = 7000;
    private const ushort NumericRegisterBaseAddress = 3451;
    private const ushort StringRegisterBaseAddress = 5891;
    private const int MinimumStringRegisterNumber = 1;
    private const int MaximumStringRegisterNumber = 6;
    private const int StringRegisterStrideWords = 40;
    private const int MinimumNumericRegisterNumber = 1;
    private const int MaximumNumericRegisterNumber = 10;
    private const int NumericRegisterStrideWords = 2;

    private FanucAddress(FanucSelector selector, ushort address, bool isBit)
    {
        Selector = selector;
        Address = address;
        IsBit = isBit;
    }

    /// <summary>대상 메모리 셀렉터입니다.</summary>
    public FanucSelector Selector { get; }

    /// <summary>변환된 1-based 주소입니다.</summary>
    public ushort Address { get; }

    /// <summary>비트 영역 주소인지 여부입니다.</summary>
    public bool IsBit { get; }

    /// <summary>주소 문자열을 지정된 연산 모드(비트/워드)로 해석합니다.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="address"/>가 null일 때.</exception>
    /// <exception cref="FormatException">주소 형식이 올바르지 않거나 연산 모드와 맞지 않을 때.</exception>
    public static FanucAddress Parse(string address, bool isBit)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (!TryParse(address, isBit, out FanucAddress? parsed))
        {
            throw new FormatException($"유효하지 않은 FANUC {(isBit ? "비트" : "워드")} 주소입니다: '{address}'.");
        }

        return parsed;
    }

    /// <summary>주소 문자열을 예외 없이 <see cref="FanucAddress"/>로 해석합니다.</summary>
    public static bool TryParse(string? address, bool isBit, [NotNullWhen(true)] out FanucAddress? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        string upper = address!.Trim().ToUpperInvariant();
        return isBit ? TryParseBitAddress(upper, out result) : TryParseWordAddress(upper, out result);
    }

    private static bool TryParseBitAddress(string upper, [NotNullWhen(true)] out FanucAddress? result)
    {
        result = null;
        if (TryExtractNumber(upper, "SDO", out int sdoNumber))
        {
            if (sdoNumber < SdoSplitThreshold)
            {
                return TryCreate(FanucSelector.InputRelay, sdoNumber, isBit: true, out result);
            }

            return TryCreate(FanucSelector.InternalRelay, sdoNumber - SdoHighOffset, isBit: true, out result);
        }

        if (TryExtractNumber(upper, "SDI", out int sdiNumber))
        {
            return TryCreate(FanucSelector.OutputRelay, sdiNumber, isBit: true, out result);
        }

        if (TryExtractNumber(upper, "RDI", out int rdiNumber))
        {
            return TryCreate(FanucSelector.OutputRelay, rdiNumber + RdiRdoOffset, isBit: true, out result);
        }

        if (TryExtractNumber(upper, "RDO", out int rdoNumber))
        {
            return TryCreate(FanucSelector.InputRelay, rdoNumber + RdiRdoOffset, isBit: true, out result);
        }

        if (TryExtractNumber(upper, "UI", out int uiNumber))
        {
            return TryCreate(FanucSelector.OutputRelay, uiNumber + UiUoOffset, isBit: true, out result);
        }

        if (TryExtractNumber(upper, "UO", out int uoNumber))
        {
            return TryCreate(FanucSelector.InputRelay, uoNumber + UiUoOffset, isBit: true, out result);
        }

        if (TryExtractNumber(upper, "SI", out int siNumber))
        {
            return TryCreate(FanucSelector.OutputRelay, siNumber + SiSoOffset, isBit: true, out result);
        }

        if (TryExtractNumber(upper, "SO", out int soNumber))
        {
            return TryCreate(FanucSelector.InputRelay, soNumber + SiSoOffset, isBit: true, out result);
        }

        return TryParseSingleLetterBitAddress(upper, out result);
    }

    private static bool TryParseSingleLetterBitAddress(string upper, [NotNullWhen(true)] out FanucAddress? result)
    {
        result = null;
        if (TryExtractNumber(upper, "I", out int inputNumber))
        {
            return TryCreate(FanucSelector.InputRelay, inputNumber, isBit: true, out result);
        }

        if (TryExtractNumber(upper, "Q", out int outputNumber))
        {
            return TryCreate(FanucSelector.OutputRelay, outputNumber, isBit: true, out result);
        }

        if (TryExtractNumber(upper, "M", out int markerNumber))
        {
            return TryCreate(FanucSelector.InternalRelay, markerNumber, isBit: true, out result);
        }

        return false;
    }

    private static bool TryParseWordAddress(string upper, [NotNullWhen(true)] out FanucAddress? result)
    {
        result = null;
        if (TryExtractNumber(upper, "GI", out int giNumber))
        {
            return TryCreate(FanucSelector.AnalogOutput, giNumber, isBit: false, out result);
        }

        if (TryExtractNumber(upper, "GO", out int goNumber))
        {
            int mapped = goNumber < GoSplitThreshold ? goNumber : goNumber - GoHighOffset;
            return TryCreate(FanucSelector.AnalogInput, mapped, isBit: false, out result);
        }

        if (TryExtractNumber(upper, "AQ", out int aqNumber))
        {
            return TryCreate(FanucSelector.AnalogOutput, aqNumber, isBit: false, out result);
        }

        if (TryExtractNumber(upper, "AI", out int aiNumber))
        {
            return TryCreate(FanucSelector.AnalogInput, aiNumber, isBit: false, out result);
        }

        if (TryExtractNumber(upper, "SR", out int srNumber))
        {
            if (srNumber is < MinimumStringRegisterNumber or > MaximumStringRegisterNumber)
            {
                return false;
            }

            return TryCreate(
                FanucSelector.Data,
                StringRegisterBaseAddress + ((srNumber - 1) * StringRegisterStrideWords),
                isBit: false,
                out result);
        }

        if (TryExtractNumber(upper, "D", out int dataNumber))
        {
            return TryCreate(FanucSelector.Data, dataNumber, isBit: false, out result);
        }

        if (TryExtractNumber(upper, "R", out int regNumber))
        {
            if (regNumber is < MinimumNumericRegisterNumber or > MaximumNumericRegisterNumber)
            {
                return false;
            }

            return TryCreate(
                FanucSelector.Data,
                NumericRegisterBaseAddress + ((regNumber - 1) * NumericRegisterStrideWords),
                isBit: false,
                out result);
        }

        return false;
    }

    private static bool TryExtractNumber(string upper, string prefix, out int number)
    {
        number = 0;
        if (!upper.StartsWith(prefix, StringComparison.Ordinal) || upper.Length <= prefix.Length)
        {
            return false;
        }

        string digits = upper.Substring(prefix.Length);
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 1;
    }

    private static bool TryCreate(FanucSelector selector, int address, bool isBit, [NotNullWhen(true)] out FanucAddress? result)
    {
        if (address is < 1 or > ushort.MaxValue)
        {
            result = null;
            return false;
        }

        result = new FanucAddress(selector, (ushort)address, isBit);
        return true;
    }
}
