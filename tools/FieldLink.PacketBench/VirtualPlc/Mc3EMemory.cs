using System;

namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>가상 MC PLC 의 디바이스 메모리입니다. 여러 스레드에서 접근하므로 내부에서 잠급니다.</summary>
internal sealed class Mc3EMemory
{
    private readonly object _sync = new();
    private readonly ushort[] _d = new ushort[Mc3EDevice.D.GetCapacity()];
    private readonly ushort[] _w = new ushort[Mc3EDevice.W.GetCapacity()];
    private readonly ushort[] _r = new ushort[Mc3EDevice.R.GetCapacity()];
    private readonly bool[] _m = new bool[Mc3EDevice.M.GetCapacity()];
    private readonly bool[] _x = new bool[Mc3EDevice.X.GetCapacity()];
    private readonly bool[] _y = new bool[Mc3EDevice.Y.GetCapacity()];

    public int GetCapacity(Mc3EDevice device) => device.GetCapacity();

    public bool TryReadBits(Mc3EDevice device, int start, int count, bool[] destination)
    {
        if (!device.IsBit() || destination.Length < count)
        {
            return false;
        }

        lock (_sync)
        {
            bool[] source = GetBitArray(device);
            if (!IsRangeValid(start, count, source.Length))
            {
                return false;
            }

            Array.Copy(source, start, destination, 0, count);
            return true;
        }
    }

    public bool TryWriteBits(Mc3EDevice device, int start, bool[] values)
    {
        if (!device.IsBit())
        {
            return false;
        }

        lock (_sync)
        {
            bool[] target = GetBitArray(device);
            if (!IsRangeValid(start, values.Length, target.Length))
            {
                return false;
            }

            Array.Copy(values, 0, target, start, values.Length);
            return true;
        }
    }

    public bool TryReadWords(Mc3EDevice device, int start, int count, ushort[] destination)
    {
        if (device.IsBit() || destination.Length < count)
        {
            return false;
        }

        lock (_sync)
        {
            ushort[] source = GetWordArray(device);
            if (!IsRangeValid(start, count, source.Length))
            {
                return false;
            }

            Array.Copy(source, start, destination, 0, count);
            return true;
        }
    }

    public bool TryWriteWords(Mc3EDevice device, int start, ushort[] values)
    {
        if (device.IsBit())
        {
            return false;
        }

        lock (_sync)
        {
            ushort[] target = GetWordArray(device);
            if (!IsRangeValid(start, values.Length, target.Length))
            {
                return false;
            }

            Array.Copy(values, 0, target, start, values.Length);
            return true;
        }
    }

    /// <summary>비트 디바이스를 워드 단위로 읽습니다. 워드 하나가 16점이고 첫 점이 최하위 비트입니다.</summary>
    public bool TryReadBitDeviceWords(Mc3EDevice device, int startBit, int wordCount, ushort[] destination)
    {
        if (!device.IsBit() || destination.Length < wordCount)
        {
            return false;
        }

        lock (_sync)
        {
            bool[] source = GetBitArray(device);
            if (!IsRangeValid(startBit, wordCount * 16, source.Length))
            {
                return false;
            }

            for (int wordIndex = 0; wordIndex < wordCount; wordIndex++)
            {
                int value = 0;
                for (int bitOffset = 0; bitOffset < 16; bitOffset++)
                {
                    if (source[startBit + (wordIndex * 16) + bitOffset])
                    {
                        value |= 1 << bitOffset;
                    }
                }

                destination[wordIndex] = (ushort)value;
            }

            return true;
        }
    }

    /// <summary>비트 디바이스를 워드 단위로 씁니다. 워드 하나가 16점이고 첫 점이 최하위 비트입니다.</summary>
    public bool TryWriteBitDeviceWords(Mc3EDevice device, int startBit, ushort[] values)
    {
        if (!device.IsBit())
        {
            return false;
        }

        lock (_sync)
        {
            bool[] target = GetBitArray(device);
            if (!IsRangeValid(startBit, values.Length * 16, target.Length))
            {
                return false;
            }

            for (int wordIndex = 0; wordIndex < values.Length; wordIndex++)
            {
                for (int bitOffset = 0; bitOffset < 16; bitOffset++)
                {
                    target[startBit + (wordIndex * 16) + bitOffset] = (values[wordIndex] & (1 << bitOffset)) != 0;
                }
            }

            return true;
        }
    }

    public bool GetBit(Mc3EDevice device, int index)
    {
        lock (_sync)
        {
            bool[] source = GetBitArray(device);
            return index >= 0 && index < source.Length && source[index];
        }
    }

    public void SetBit(Mc3EDevice device, int index, bool value)
    {
        lock (_sync)
        {
            bool[] target = GetBitArray(device);
            if (index >= 0 && index < target.Length)
            {
                target[index] = value;
            }
        }
    }

    public ushort GetWord(Mc3EDevice device, int index)
    {
        lock (_sync)
        {
            ushort[] source = GetWordArray(device);
            return index >= 0 && index < source.Length ? source[index] : (ushort)0;
        }
    }

    public void SetWord(Mc3EDevice device, int index, ushort value)
    {
        lock (_sync)
        {
            ushort[] target = GetWordArray(device);
            if (index >= 0 && index < target.Length)
            {
                target[index] = value;
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            Array.Clear(_d);
            Array.Clear(_w);
            Array.Clear(_r);
            Array.Clear(_m);
            Array.Clear(_x);
            Array.Clear(_y);
        }
    }

    public void FillTestPattern()
    {
        lock (_sync)
        {
            for (int i = 0; i < _d.Length; i++)
            {
                _d[i] = (ushort)(i & 0xFFFF);
            }

            for (int i = 0; i < _w.Length; i++)
            {
                _w[i] = (ushort)((i * 2) & 0xFFFF);
            }

            for (int i = 0; i < _r.Length; i++)
            {
                _r[i] = (ushort)((i * 4) & 0xFFFF);
            }

            for (int i = 0; i < _m.Length; i++)
            {
                _m[i] = i % 2 == 0;
            }

            for (int i = 0; i < _x.Length; i++)
            {
                _x[i] = i % 3 == 0;
            }

            for (int i = 0; i < _y.Length; i++)
            {
                _y[i] = i % 5 == 0;
            }
        }
    }

    private bool[] GetBitArray(Mc3EDevice device) => device switch
    {
        Mc3EDevice.M => _m,
        Mc3EDevice.X => _x,
        Mc3EDevice.Y => _y,
        _ => _m,
    };

    private ushort[] GetWordArray(Mc3EDevice device) => device switch
    {
        Mc3EDevice.D => _d,
        Mc3EDevice.W => _w,
        Mc3EDevice.R => _r,
        _ => _d,
    };

    private static bool IsRangeValid(int start, int count, int capacity)
        => start >= 0 && count >= 0 && start <= capacity - count;
}
