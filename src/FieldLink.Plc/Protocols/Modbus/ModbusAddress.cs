using System;
using System.Diagnostics.CodeAnalysis;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modicon 5자리 또는 6자리 표기법으로 작성된 Modbus 참조 주소를 해석한 불변 값 객체입니다. (사양서 §2)</summary>
public sealed class ModbusAddress
{
    private const int ShortAddressLength = 5;
    private const int LongAddressLength = 6;
    private const int ShortAreaBaseOffset = 10000;
    private const int LongAreaBaseOffset = 100000;
    private const int InputRegisterPrefixMultiplier = 3;
    private const int HoldingRegisterPrefixMultiplier = 4;
    private const int MinElementNumber = 1;
    private const int MaxElementNumber = 65536;
    private const char CoilAreaPrefix = '0';
    private const char DiscreteInputAreaPrefix = '1';
    private const char InputRegisterAreaPrefix = '3';
    private const char HoldingRegisterAreaPrefix = '4';

    private ModbusAddress(string originalAddress, ModbusArea area, int elementNumber, ushort pduAddress)
    {
        OriginalAddress = originalAddress;
        Area = area;
        ElementNumber = elementNumber;
        PduAddress = pduAddress;
    }

    /// <summary>해석 전 원본 주소 문자열입니다.</summary>
    public string OriginalAddress { get; }

    /// <summary>Modbus 대상 메모리 영역입니다.</summary>
    public ModbusArea Area { get; }

    /// <summary>1부터 시작하는 원소 번호입니다. (1~65536)</summary>
    public int ElementNumber { get; }

    /// <summary>PDU 프레임에 실리는 0부터 시작하는 16비트 주소입니다. (0~65535)</summary>
    public ushort PduAddress { get; }

    /// <summary>이 영역이 쓰기 가능한 영역(코일 또는 보유 레지스터)인지 여부입니다.</summary>
    public bool IsWritable => Area is ModbusArea.Coil or ModbusArea.HoldingRegister;

    /// <summary>이 영역이 비트 단위(1비트) 접근 영역인지 여부입니다.</summary>
    public bool IsBit => Area is ModbusArea.Coil or ModbusArea.DiscreteInput;

    /// <summary>주소 문자열을 해석하여 <see cref="ModbusAddress"/> 인스턴스를 반환합니다.</summary>
    /// <param name="address">Modicon 5자리 또는 6자리 주소입니다. (예: "40001", "000020")</param>
    /// <returns>해석된 주소 객체입니다.</returns>
    /// <exception cref="ArgumentException"><paramref name="address"/>가 null이거나 공백일 때.</exception>
    /// <exception cref="FormatException">주소 형식 또는 범위가 올바르지 않을 때.</exception>
    public static ModbusAddress Parse(string address)
    {
        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소 문자열은 필수입니다.", nameof(address));
        }

        if (!TryParse(address, out ModbusAddress? result, out string? errorMessage))
        {
            throw new FormatException(errorMessage);
        }

        return result;
    }

    /// <summary>주소 문자열 해석을 시도합니다.</summary>
    /// <param name="address">해석할 주소 문자열입니다.</param>
    /// <param name="result">성공 시 해석된 주소 객체입니다.</param>
    /// <returns>성공 시 true, 실패 시 false입니다.</returns>
    public static bool TryParse(string? address, [NotNullWhen(true)] out ModbusAddress? result)
    {
        return TryParse(address, out result, out _);
    }

    private static bool TryParse(string? address, [NotNullWhen(true)] out ModbusAddress? result, out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (address is null || address.Length is not (ShortAddressLength or LongAddressLength))
        {
            errorMessage = $"Modbus 주소 길이는 {ShortAddressLength}자리 또는 {LongAddressLength}자리여야 합니다. (입력: {address})";
            return false;
        }

        for (int i = 0; i < address.Length; i++)
        {
            if (address[i] is < '0' or > '9')
            {
                errorMessage = $"Modbus 주소는 숫자로만 구성되어야 합니다. (입력: {address})";
                return false;
            }
        }

        int rawValue = int.Parse(address);
        char prefix = address[0];
        int baseOffset = address.Length == ShortAddressLength ? ShortAreaBaseOffset : LongAreaBaseOffset;

        ModbusArea area;
        int elementNumber;

        switch (prefix)
        {
            case CoilAreaPrefix:
                area = ModbusArea.Coil;
                elementNumber = rawValue;
                break;
            case DiscreteInputAreaPrefix:
                area = ModbusArea.DiscreteInput;
                elementNumber = rawValue - baseOffset;
                break;
            case InputRegisterAreaPrefix:
                area = ModbusArea.InputRegister;
                elementNumber = rawValue - (InputRegisterPrefixMultiplier * baseOffset);
                break;
            case HoldingRegisterAreaPrefix:
                area = ModbusArea.HoldingRegister;
                elementNumber = rawValue - (HoldingRegisterPrefixMultiplier * baseOffset);
                break;
            default:
                errorMessage = $"지원하지 않는 Modbus 영역 접두사('{prefix}')입니다. 0, 1, 3, 4만 지원합니다.";
                return false;
        }

        if (elementNumber is < MinElementNumber or > MaxElementNumber)
        {
            errorMessage = $"원소 번호({elementNumber})는 {MinElementNumber}에서 {MaxElementNumber} 사이여야 합니다. (입력: {address})";
            return false;
        }

        ushort pduAddress = (ushort)(elementNumber - 1);
        result = new ModbusAddress(address, area, elementNumber, pduAddress);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => OriginalAddress;
}
