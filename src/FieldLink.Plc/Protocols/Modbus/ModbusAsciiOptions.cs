using System;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modbus ASCII 프로토콜의 통신 옵션입니다.</summary>
public sealed class ModbusAsciiOptions
{
    private const byte DefaultSlaveAddress = 1;
    private const byte MinSlaveAddress = 1;
    private const byte MaxSlaveAddress = 247;
    private const ushort MinCount = 1;
    private const ushort DefaultMaxReadRegisters = 125;
    private const ushort DefaultMaxWriteRegisters = 123;
    private const ushort DefaultMaxReadCoils = 2000;
    private const ushort DefaultMaxWriteCoils = 1968;

    private readonly ushort _maxReadRegisters = DefaultMaxReadRegisters;
    private readonly ushort _maxWriteRegisters = DefaultMaxWriteRegisters;
    private readonly ushort _maxReadCoils = DefaultMaxReadCoils;
    private readonly ushort _maxWriteCoils = DefaultMaxWriteCoils;

    /// <summary>슬레이브 국번으로 ASCII 옵션을 초기화합니다.</summary>
    /// <param name="slaveAddress">슬레이브 국번(1~247)입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">국번이 1~247 범위를 벗어났을 때.</exception>
    public ModbusAsciiOptions(byte slaveAddress = DefaultSlaveAddress)
    {
        if (slaveAddress is < MinSlaveAddress or > MaxSlaveAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(slaveAddress), slaveAddress, $"Modbus ASCII 국번은 {MinSlaveAddress}에서 {MaxSlaveAddress} 사이여야 합니다.");
        }

        SlaveAddress = slaveAddress;
        UseSingleWriteFunctions = false;
    }

    /// <summary>대상 슬레이브 장치의 국번입니다. (1~247)</summary>
    public byte SlaveAddress { get; }

    /// <summary>1회 요청당 최대 보유/입력 레지스터 읽기 수량입니다. (1~125)</summary>
    public ushort MaxReadRegisters
    {
        get => _maxReadRegisters;
        init => _maxReadRegisters = ValidateRange(value, DefaultMaxReadRegisters, nameof(value), "최대 읽기 레지스터 수");
    }

    /// <summary>1회 요청당 최대 보유 레지스터 쓰기 수량입니다. (1~123)</summary>
    public ushort MaxWriteRegisters
    {
        get => _maxWriteRegisters;
        init => _maxWriteRegisters = ValidateRange(value, DefaultMaxWriteRegisters, nameof(value), "최대 쓰기 레지스터 수");
    }

    /// <summary>1회 요청당 최대 코일/이산입력 읽기 수량입니다. (1~2000)</summary>
    public ushort MaxReadCoils
    {
        get => _maxReadCoils;
        init => _maxReadCoils = ValidateRange(value, DefaultMaxReadCoils, nameof(value), "최대 읽기 코일 수");
    }

    /// <summary>1회 요청당 최대 코일 쓰기 수량입니다. (1~1968)</summary>
    public ushort MaxWriteCoils
    {
        get => _maxWriteCoils;
        init => _maxWriteCoils = ValidateRange(value, DefaultMaxWriteCoils, nameof(value), "최대 쓰기 코일 수");
    }

    /// <summary>단일 원소 쓰기 시 FC 0x05/0x06을 사용할지 여부입니다. 기본값은 false입니다.</summary>
    public bool UseSingleWriteFunctions { get; init; }

    private static ushort ValidateRange(ushort value, ushort maxValue, string paramName, string label)
    {
        if (value < MinCount || value > maxValue)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{label}는 {MinCount}에서 {maxValue} 사이여야 합니다.");
        }

        return value;
    }
}
