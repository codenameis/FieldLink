using System;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>가상 PLC 의 4개 메모리 영역입니다. 여러 스레드에서 접근하므로 내부에서 잠급니다.</summary>
internal sealed class VirtualPlcMemory
{
    private readonly object _sync = new();
    private readonly bool[] _coils;
    private readonly bool[] _discreteInputs;
    private readonly ushort[] _holdingRegisters;
    private readonly ushort[] _inputRegisters;

    public VirtualPlcMemory(int coilCount, int discreteInputCount, int holdingRegisterCount, int inputRegisterCount)
    {
        if (coilCount < 1 || discreteInputCount < 1 || holdingRegisterCount < 1 || inputRegisterCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(coilCount), "메모리 크기는 1 이상이어야 합니다.");
        }

        _coils = new bool[coilCount];
        _discreteInputs = new bool[discreteInputCount];
        _holdingRegisters = new ushort[holdingRegisterCount];
        _inputRegisters = new ushort[inputRegisterCount];
    }

    public int CoilCount => _coils.Length;

    public int DiscreteInputCount => _discreteInputs.Length;

    public int HoldingRegisterCount => _holdingRegisters.Length;

    public int InputRegisterCount => _inputRegisters.Length;

    public int GetCount(ModbusArea area) => area switch
    {
        ModbusArea.Coil => _coils.Length,
        ModbusArea.DiscreteInput => _discreteInputs.Length,
        ModbusArea.InputRegister => _inputRegisters.Length,
        ModbusArea.HoldingRegister => _holdingRegisters.Length,
        _ => 0,
    };

    public bool IsWritable(ModbusArea area) => area is ModbusArea.Coil or ModbusArea.HoldingRegister;

    public bool TryReadBits(ModbusArea area, int start, int count, bool[] destination)
    {
        bool[] source = area == ModbusArea.Coil ? _coils : _discreteInputs;
        if (area is not (ModbusArea.Coil or ModbusArea.DiscreteInput) || destination.Length < count)
        {
            return false;
        }

        lock (_sync)
        {
            if (!IsRangeValid(start, count, source.Length))
            {
                return false;
            }

            Array.Copy(source, start, destination, 0, count);
            return true;
        }
    }

    public bool TryWriteBits(int start, bool[] values)
    {
        lock (_sync)
        {
            if (!IsRangeValid(start, values.Length, _coils.Length))
            {
                return false;
            }

            Array.Copy(values, 0, _coils, start, values.Length);
            return true;
        }
    }

    public bool TryReadRegisters(ModbusArea area, int start, int count, ushort[] destination)
    {
        ushort[] source = area == ModbusArea.HoldingRegister ? _holdingRegisters : _inputRegisters;
        if (area is not (ModbusArea.HoldingRegister or ModbusArea.InputRegister) || destination.Length < count)
        {
            return false;
        }

        lock (_sync)
        {
            if (!IsRangeValid(start, count, source.Length))
            {
                return false;
            }

            Array.Copy(source, start, destination, 0, count);
            return true;
        }
    }

    public bool TryWriteRegisters(int start, ushort[] values)
    {
        lock (_sync)
        {
            if (!IsRangeValid(start, values.Length, _holdingRegisters.Length))
            {
                return false;
            }

            Array.Copy(values, 0, _holdingRegisters, start, values.Length);
            return true;
        }
    }

    public ushort GetRegister(ModbusArea area, int index)
    {
        lock (_sync)
        {
            ushort[] source = area == ModbusArea.HoldingRegister ? _holdingRegisters : _inputRegisters;
            return index >= 0 && index < source.Length ? source[index] : (ushort)0;
        }
    }

    public void SetRegister(ModbusArea area, int index, ushort value)
    {
        lock (_sync)
        {
            ushort[] target = area == ModbusArea.HoldingRegister ? _holdingRegisters : _inputRegisters;
            if (index >= 0 && index < target.Length)
            {
                target[index] = value;
            }
        }
    }

    public bool GetCoil(int index)
    {
        lock (_sync)
        {
            return index >= 0 && index < _coils.Length && _coils[index];
        }
    }

    public bool GetBit(ModbusArea area, int index)
    {
        lock (_sync)
        {
            bool[] source = area == ModbusArea.Coil ? _coils : _discreteInputs;
            return index >= 0 && index < source.Length && source[index];
        }
    }

    public void SetCoil(int index, bool value)
    {
        lock (_sync)
        {
            if (index >= 0 && index < _coils.Length)
            {
                _coils[index] = value;
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            Array.Clear(_coils, 0, _coils.Length);
            Array.Clear(_discreteInputs, 0, _discreteInputs.Length);
            Array.Clear(_holdingRegisters, 0, _holdingRegisters.Length);
            Array.Clear(_inputRegisters, 0, _inputRegisters.Length);
        }
    }

    /// <summary>값이 눈에 띄도록 영역마다 다른 규칙으로 채웁니다.</summary>
    public void FillTestPattern()
    {
        lock (_sync)
        {
            for (int i = 0; i < _holdingRegisters.Length; i++)
            {
                _holdingRegisters[i] = (ushort)(i & 0xFFFF);
            }

            for (int i = 0; i < _inputRegisters.Length; i++)
            {
                _inputRegisters[i] = (ushort)((i + 1000) & 0xFFFF);
            }

            for (int i = 0; i < _coils.Length; i++)
            {
                _coils[i] = i % 2 == 0;
            }

            for (int i = 0; i < _discreteInputs.Length; i++)
            {
                _discreteInputs[i] = i % 3 == 0;
            }
        }
    }

    private static bool IsRangeValid(int start, int count, int capacity)
        => start >= 0 && count >= 0 && start <= capacity - count;
}
