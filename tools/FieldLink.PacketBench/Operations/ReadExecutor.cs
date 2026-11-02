using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc;
using FieldLink.Plc.Conversion;

namespace FieldLink.PacketBench.Operations;

/// <summary>주소·단위·개수를 실제 클라이언트 호출로 옮기고 결과를 표로 만듭니다.</summary>
internal static class ReadExecutor
{
    public static async Task<PlcResult<ReadOutcome>> ExecuteAsync(
        PlcClient client,
        string address,
        ReadInterpretation interpretation,
        CountUnit unit,
        int count,
        ByteOrder byteOrder,
        CancellationToken cancellationToken)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("주소를 입력하세요.", nameof(address));
        }

        (int elementCount, int byteCount) = ResolveLayout(interpretation, unit, count);

        switch (interpretation)
        {
            case ReadInterpretation.Bit:
                return await ReadBitsAsync(client, address, elementCount, byteCount, cancellationToken).ConfigureAwait(true);
            case ReadInterpretation.Word:
                return await ReadWordsAsync(client, address, elementCount, byteCount, byteOrder, cancellationToken).ConfigureAwait(true);
            case ReadInterpretation.Int16:
                return await ReadInt16Async(client, address, elementCount, byteCount, cancellationToken).ConfigureAwait(true);
            case ReadInterpretation.DWord:
                return await ReadUInt32Async(client, address, elementCount, byteCount, cancellationToken).ConfigureAwait(true);
            case ReadInterpretation.Int32:
                return await ReadInt32Async(client, address, elementCount, byteCount, cancellationToken).ConfigureAwait(true);
            case ReadInterpretation.Float32:
                return await ReadSingleAsync(client, address, elementCount, byteCount, cancellationToken).ConfigureAwait(true);
            case ReadInterpretation.Float64:
                return await ReadDoubleAsync(client, address, elementCount, byteCount, cancellationToken).ConfigureAwait(true);
            default:
                return await ReadRawBytesAsync(client, address, byteCount, byteOrder, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>단위와 해석 형식으로 원소 수와 바이트 수를 정합니다.</summary>
    public static (int ElementCount, int ByteCount) ResolveLayout(ReadInterpretation interpretation, CountUnit unit, int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "개수는 1 이상이어야 합니다.");
        }

        if (interpretation == ReadInterpretation.Bit)
        {
            if (unit != CountUnit.Bit)
            {
                throw new ArgumentException("비트 해석은 비트 단위로만 읽을 수 있습니다.", nameof(unit));
            }

            return (count, (count + 7) / 8);
        }

        int byteCount = unit switch
        {
            CountUnit.Word => checked(count * 2),
            CountUnit.Byte => count,
            _ => throw new ArgumentException("비트 단위는 비트 해석에서만 쓸 수 있습니다.", nameof(unit)),
        };

        if (interpretation == ReadInterpretation.RawBytes)
        {
            return (byteCount, byteCount);
        }

        int elementSize = GetElementSize(interpretation);
        if (byteCount % elementSize != 0)
        {
            throw new ArgumentException(
                $"요청한 {byteCount}바이트가 {elementSize}바이트 원소로 나누어지지 않습니다. 개수를 조정하세요.",
                nameof(count));
        }

        return (byteCount / elementSize, byteCount);
    }

    private static int GetElementSize(ReadInterpretation interpretation) => interpretation switch
    {
        ReadInterpretation.Word or ReadInterpretation.Int16 => 2,
        ReadInterpretation.DWord or ReadInterpretation.Int32 or ReadInterpretation.Float32 => 4,
        ReadInterpretation.Float64 => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(interpretation), interpretation, "지원하지 않는 해석 형식입니다."),
    };

    private static async Task<PlcResult<ReadOutcome>> ReadBitsAsync(
        PlcClient client,
        string address,
        int elementCount,
        int byteCount,
        CancellationToken cancellationToken)
    {
        PlcResult<bool[]> result = await client.ReadBooleanAsync(address, elementCount, cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            return PlcResult.Failure<ReadOutcome>(result.Error);
        }

        bool[] bits = result.Value;
        var rows = new List<ReadRow>(bits.Length);
        var rawBytes = new byte[bits.Length];

        for (int i = 0; i < bits.Length; i++)
        {
            rawBytes[i] = bits[i] ? (byte)1 : (byte)0;
            string text = bits[i] ? "1" : "0";
            rows.Add(new ReadRow(
                AddressIncrementer.Increment(address, i),
                text,
                bits[i] ? "01" : "00",
                text,
                bits[i] ? "1" : "0"));
        }

        return PlcResult.Success(new ReadOutcome(rows, rawBytes, bits.Length, byteCount, $"비트 {bits.Length}개"));
    }

    private static async Task<PlcResult<ReadOutcome>> ReadWordsAsync(
        PlcClient client,
        string address,
        int elementCount,
        int byteCount,
        ByteOrder byteOrder,
        CancellationToken cancellationToken)
    {
        PlcResult<ushort[]> result = await client.ReadUInt16Async(address, elementCount, cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            return PlcResult.Failure<ReadOutcome>(result.Error);
        }

        ushort[] values = result.Value;
        var rows = new List<ReadRow>(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            rows.Add(new ReadRow(
                AddressIncrementer.Increment(address, i),
                values[i].ToString(CultureInfo.InvariantCulture),
                values[i].ToString("X4", CultureInfo.InvariantCulture),
                ToBinary(values[i], 16),
                ToAscii(PlcValueConverter.GetBytes(values[i], byteOrder))));
        }

        return PlcResult.Success(new ReadOutcome(
            rows,
            PlcValueConverter.GetBytes(values, byteOrder),
            values.Length,
            byteCount,
            $"워드 {values.Length}개 · {byteCount}바이트"));
    }

    private static async Task<PlcResult<ReadOutcome>> ReadInt16Async(
        PlcClient client,
        string address,
        int elementCount,
        int byteCount,
        CancellationToken cancellationToken)
    {
        PlcResult<short[]> result = await client.ReadInt16Async(address, elementCount, cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            return PlcResult.Failure<ReadOutcome>(result.Error);
        }

        short[] values = result.Value;
        var rows = new List<ReadRow>(values.Length);
        var rawBytes = new byte[values.Length * 2];

        for (int i = 0; i < values.Length; i++)
        {
            ushort bits = unchecked((ushort)values[i]);
            rawBytes[i * 2] = (byte)(bits >> 8);
            rawBytes[(i * 2) + 1] = (byte)(bits & 0xFF);
            rows.Add(new ReadRow(
                AddressIncrementer.Increment(address, i),
                values[i].ToString(CultureInfo.InvariantCulture),
                bits.ToString("X4", CultureInfo.InvariantCulture),
                ToBinary(bits, 16),
                ToAscii(new[] { rawBytes[i * 2], rawBytes[(i * 2) + 1] })));
        }

        return PlcResult.Success(new ReadOutcome(rows, rawBytes, values.Length, byteCount, $"INT16 {values.Length}개 · {byteCount}바이트"));
    }

    private static async Task<PlcResult<ReadOutcome>> ReadUInt32Async(
        PlcClient client,
        string address,
        int elementCount,
        int byteCount,
        CancellationToken cancellationToken)
    {
        PlcResult<uint[]> result = await client.ReadUInt32Async(address, elementCount, cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            return PlcResult.Failure<ReadOutcome>(result.Error);
        }

        uint[] values = result.Value;
        var rows = new List<ReadRow>(values.Length);
        var rawBytes = new byte[values.Length * 4];

        for (int i = 0; i < values.Length; i++)
        {
            WriteBigEndian(values[i], rawBytes, i * 4, 4);
            rows.Add(new ReadRow(
                AddressIncrementer.Increment(address, i),
                values[i].ToString(CultureInfo.InvariantCulture),
                values[i].ToString("X8", CultureInfo.InvariantCulture),
                ToBinary(values[i], 32),
                ToAscii(rawBytes[(i * 4)..((i * 4) + 4)])));
        }

        return PlcResult.Success(new ReadOutcome(rows, rawBytes, values.Length, byteCount, $"DWORD {values.Length}개 · {byteCount}바이트"));
    }

    private static async Task<PlcResult<ReadOutcome>> ReadInt32Async(
        PlcClient client,
        string address,
        int elementCount,
        int byteCount,
        CancellationToken cancellationToken)
    {
        PlcResult<int[]> result = await client.ReadInt32Async(address, elementCount, cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            return PlcResult.Failure<ReadOutcome>(result.Error);
        }

        int[] values = result.Value;
        var rows = new List<ReadRow>(values.Length);
        var rawBytes = new byte[values.Length * 4];

        for (int i = 0; i < values.Length; i++)
        {
            uint bits = unchecked((uint)values[i]);
            WriteBigEndian(bits, rawBytes, i * 4, 4);
            rows.Add(new ReadRow(
                AddressIncrementer.Increment(address, i),
                values[i].ToString(CultureInfo.InvariantCulture),
                bits.ToString("X8", CultureInfo.InvariantCulture),
                ToBinary(bits, 32),
                ToAscii(rawBytes[(i * 4)..((i * 4) + 4)])));
        }

        return PlcResult.Success(new ReadOutcome(rows, rawBytes, values.Length, byteCount, $"DINT {values.Length}개 · {byteCount}바이트"));
    }

    private static async Task<PlcResult<ReadOutcome>> ReadSingleAsync(
        PlcClient client,
        string address,
        int elementCount,
        int byteCount,
        CancellationToken cancellationToken)
    {
        PlcResult<float[]> result = await client.ReadSingleAsync(address, elementCount, cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            return PlcResult.Failure<ReadOutcome>(result.Error);
        }

        float[] values = result.Value;
        var rows = new List<ReadRow>(values.Length);
        var rawBytes = new byte[values.Length * 4];

        for (int i = 0; i < values.Length; i++)
        {
            uint bits = BitConverter.SingleToUInt32Bits(values[i]);
            WriteBigEndian(bits, rawBytes, i * 4, 4);
            rows.Add(new ReadRow(
                AddressIncrementer.Increment(address, i),
                values[i].ToString("R", CultureInfo.InvariantCulture),
                bits.ToString("X8", CultureInfo.InvariantCulture),
                ToBinary(bits, 32),
                ToAscii(rawBytes[(i * 4)..((i * 4) + 4)])));
        }

        return PlcResult.Success(new ReadOutcome(rows, rawBytes, values.Length, byteCount, $"REAL {values.Length}개 · {byteCount}바이트"));
    }

    private static async Task<PlcResult<ReadOutcome>> ReadDoubleAsync(
        PlcClient client,
        string address,
        int elementCount,
        int byteCount,
        CancellationToken cancellationToken)
    {
        PlcResult<double[]> result = await client.ReadDoubleAsync(address, elementCount, cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            return PlcResult.Failure<ReadOutcome>(result.Error);
        }

        double[] values = result.Value;
        var rows = new List<ReadRow>(values.Length);
        var rawBytes = new byte[values.Length * 8];

        for (int i = 0; i < values.Length; i++)
        {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(values[i]));
            WriteBigEndian(bits, rawBytes, i * 8, 8);
            rows.Add(new ReadRow(
                AddressIncrementer.Increment(address, i),
                values[i].ToString("R", CultureInfo.InvariantCulture),
                bits.ToString("X16", CultureInfo.InvariantCulture),
                ToBinary(bits, 64),
                ToAscii(rawBytes[(i * 8)..((i * 8) + 8)])));
        }

        return PlcResult.Success(new ReadOutcome(rows, rawBytes, values.Length, byteCount, $"LREAL {values.Length}개 · {byteCount}바이트"));
    }

    private static async Task<PlcResult<ReadOutcome>> ReadRawBytesAsync(
        PlcClient client,
        string address,
        int byteCount,
        ByteOrder byteOrder,
        CancellationToken cancellationToken)
    {
        // 바이트 하나를 원소로 쓰지 않는 프로토콜이 많으므로 워드로 읽고 앞에서부터 잘라 쓴다.
        int wordCount = (byteCount + 1) / 2;
        PlcResult<ushort[]> result = await client.ReadUInt16Async(address, wordCount, cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            return PlcResult.Failure<ReadOutcome>(result.Error);
        }

        byte[] allBytes = PlcValueConverter.GetBytes(result.Value, byteOrder);
        var rawBytes = new byte[byteCount];
        Array.Copy(allBytes, rawBytes, byteCount);

        var rows = new List<ReadRow>(byteCount);
        for (int i = 0; i < byteCount; i++)
        {
            rows.Add(new ReadRow(
                AddressIncrementer.Increment(address, i),
                rawBytes[i].ToString(CultureInfo.InvariantCulture),
                rawBytes[i].ToString("X2", CultureInfo.InvariantCulture),
                ToBinary(rawBytes[i], 8),
                ToAscii(new[] { rawBytes[i] })));
        }

        return PlcResult.Success(new ReadOutcome(rows, rawBytes, byteCount, byteCount, $"바이트 {byteCount}개"));
    }

    private static void WriteBigEndian(ulong value, byte[] destination, int offset, int width)
    {
        for (int i = 0; i < width; i++)
        {
            destination[offset + i] = (byte)(value >> (8 * (width - 1 - i)));
        }
    }

    private static string ToBinary(ulong value, int width)
    {
        var characters = new char[width];
        for (int i = width - 1; i >= 0; i--)
        {
            characters[i] = (value & 1) == 1 ? '1' : '0';
            value >>= 1;
        }

        return new string(characters);
    }

    private static string ToAscii(byte[] bytes)
    {
        var characters = new char[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            characters[i] = bytes[i] is >= 0x20 and <= 0x7E ? (char)bytes[i] : '.';
        }

        return new string(characters);
    }
}
