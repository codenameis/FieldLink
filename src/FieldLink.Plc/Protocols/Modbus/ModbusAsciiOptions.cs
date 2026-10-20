using System;

namespace FieldLink.Plc.Protocols.Modbus;

/// <summary>Modbus ASCII 프로토콜의 통신 옵션입니다.</summary>
public sealed class ModbusAsciiOptions
{
    private const byte DefaultSlaveAddress = 1;
    private const ushort DefaultMaxReadRegisters = 125;
    private const ushort DefaultMaxWriteRegisters = 123;
    private const ushort DefaultMaxReadCoils = 2000;
    private const ushort DefaultMaxWriteCoils = 1968;

    /// <summary>슬레이브 국번으로 ASCII 옵션을 초기화합니다.</summary>
    /// <param name="slaveAddress">슬레이브 국번(1~247)입니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">국번이 1~247 범위를 벗어났을 때.</exception>
    public ModbusAsciiOptions(byte slaveAddress = DefaultSlaveAddress)
    {
        if (slaveAddress is < 1 or > 247)
        {
            throw new ArgumentOutOfRangeException(nameof(slaveAddress), slaveAddress, "Modbus ASCII 국번은 1에서 247 사이여야 합니다.");
        }

        SlaveAddress = slaveAddress;
        MaxReadRegisters = DefaultMaxReadRegisters;
        MaxWriteRegisters = DefaultMaxWriteRegisters;
        MaxReadCoils = DefaultMaxReadCoils;
        MaxWriteCoils = DefaultMaxWriteCoils;
        UseSingleWriteFunctions = false;
    }

    /// <summary>대상 슬레이브 장치의 국번입니다. (1~247)</summary>
    public byte SlaveAddress { get; }

    /// <summary>1회 요청당 최대 보유/입력 레지스터 읽기 수량입니다. (1~125)</summary>
    public ushort MaxReadRegisters { get; init; }

    /// <summary>1회 요청당 최대 보유 레지스터 쓰기 수량입니다. (1~123)</summary>
    public ushort MaxWriteRegisters { get; init; }

    /// <summary>1회 요청당 최대 코일/이산입력 읽기 수량입니다. (1~2000)</summary>
    public ushort MaxReadCoils { get; init; }

    /// <summary>1회 요청당 최대 코일 쓰기 수량입니다. (1~1968)</summary>
    public ushort MaxWriteCoils { get; init; }

    /// <summary>단일 원소 쓰기 시 FC 0x05/0x06을 사용할지 여부입니다. 기본값은 false입니다.</summary>
    public bool UseSingleWriteFunctions { get; init; }
}
