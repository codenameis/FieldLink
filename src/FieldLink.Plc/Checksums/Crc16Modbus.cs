using System;

namespace FieldLink.Plc.Checksums;

/// <summary>Modbus RTU 규격(다항식 0xA001, 초깃값 0xFFFF)에 따른 CRC-16 체크섬을 계산합니다.</summary>
/// <remarks>
/// <para>Modbus over Serial Line Specification V1.02 §5.1 사양을 따릅니다.</para>
/// <para>전송 순서는 하위 바이트(Low Byte) 먼저, 상위 바이트(High Byte) 나중입니다.</para>
/// </remarks>
public static class Crc16Modbus
{
    private const ushort InitialValue = 0xFFFF;
    private const ushort Polynomial = 0xA001;
    private const int LookupTableSize = 256;
    private const int BitsPerByte = 8;
    private const ushort LowestBitMask = 0x0001;
    private const ushort ByteMask = 0xFF;

    private static readonly ushort[] LookupTable = CreateLookupTable();

    /// <summary>지정된 바이트 배열의 CRC-16 값을 계산합니다.</summary>
    /// <param name="buffer">체크섬을 계산할 데이터 배열입니다.</param>
    /// <param name="offset">계산을 시작할 배열 오프셋입니다.</param>
    /// <param name="count">계산에 포함할 바이트 수입니다.</param>
    /// <returns>계산된 16비트 CRC 값입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/>가 null일 때.</exception>
    /// <exception cref="ArgumentOutOfRangeException">오프셋 또는 바이트 수가 유효하지 않을 때.</exception>
    public static ushort Calculate(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "오프셋은 0 이상이고 배열 길이 이하여야 합니다.");
        }

        if (count < 0 || count > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "바이트 수는 0 이상이고 배열 범위를 벗어날 수 없습니다.");
        }

        ushort crc = InitialValue;
        for (int i = 0; i < count; i++)
        {
            byte index = (byte)(crc ^ buffer[offset + i]);
            crc = (ushort)((crc >> 8) ^ LookupTable[index]);
        }

        return crc;
    }

    /// <summary>전체 바이트 배열의 CRC-16 값을 계산합니다.</summary>
    /// <param name="buffer">체크섬을 계산할 데이터 배열입니다.</param>
    /// <returns>계산된 16비트 CRC 값입니다.</returns>
    public static ushort Calculate(byte[] buffer)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        return Calculate(buffer, 0, buffer.Length);
    }

    /// <summary>Modbus RTU 전송 순서(하위 바이트 먼저, 상위 바이트 나중)에 맞는 2바이트 배열을 반환합니다.</summary>
    /// <param name="buffer">체크섬을 계산할 데이터 배열입니다.</param>
    /// <param name="offset">계산을 시작할 배열 오프셋입니다.</param>
    /// <param name="count">계산에 포함할 바이트 수입니다.</param>
    /// <returns>하위 바이트, 상위 바이트 순서의 2바이트 배열입니다.</returns>
    public static byte[] CalculateBytes(byte[] buffer, int offset, int count)
    {
        ushort crc = Calculate(buffer, offset, count);
        return new byte[]
        {
            (byte)(crc & ByteMask),
            (byte)((crc >> BitsPerByte) & ByteMask)
        };
    }

    private static ushort[] CreateLookupTable()
    {
        var table = new ushort[LookupTableSize];
        for (int i = 0; i < LookupTableSize; i++)
        {
            ushort value = (ushort)i;
            for (int bit = 0; bit < BitsPerByte; bit++)
            {
                if ((value & LowestBitMask) != 0)
                {
                    value = (ushort)((value >> 1) ^ Polynomial);
                }
                else
                {
                    value = (ushort)(value >> 1);
                }
            }

            table[i] = value;
        }

        return table;
    }
}
