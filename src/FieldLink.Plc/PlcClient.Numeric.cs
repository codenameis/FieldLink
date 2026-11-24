using System.Threading;
using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Conversion;

namespace FieldLink.Plc;

public sealed partial class PlcClient
{
    /// <summary>16비트 부호 있는 정수 한 개를 읽습니다.</summary>
    public Task<PlcResult<short>> ReadInt16Async(string address, CancellationToken cancellationToken = default)
    {
        return ReadScalarAsync(address, PlcDataType.Int16, PlcValueConverter.ToInt16, cancellationToken);
    }

    /// <summary>연속된 16비트 부호 있는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<short[]>> ReadInt16Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadArrayAsync(address, PlcDataType.Int16, count, PlcValueConverter.ToInt16Array, cancellationToken);
    }

    /// <summary>16비트 부호 있는 정수 한 개를 씁니다.</summary>
    public Task<PlcResult> WriteInt16Async(string address, short value, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Int16, PlcValueConverter.GetBytes(value, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 16비트 부호 있는 정수 배열을 씁니다.</summary>
    public Task<PlcResult> WriteInt16Async(string address, short[] values, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Int16, PlcValueConverter.GetBytes(values, _byteOrder), cancellationToken);
    }

    /// <summary>16비트 부호 없는 정수 한 개를 읽습니다.</summary>
    public Task<PlcResult<ushort>> ReadUInt16Async(string address, CancellationToken cancellationToken = default)
    {
        return ReadScalarAsync(address, PlcDataType.UInt16, PlcValueConverter.ToUInt16, cancellationToken);
    }

    /// <summary>연속된 16비트 부호 없는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<ushort[]>> ReadUInt16Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadArrayAsync(address, PlcDataType.UInt16, count, PlcValueConverter.ToUInt16Array, cancellationToken);
    }

    /// <summary>16비트 부호 없는 정수 한 개를 씁니다.</summary>
    public Task<PlcResult> WriteUInt16Async(string address, ushort value, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.UInt16, PlcValueConverter.GetBytes(value, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 16비트 부호 없는 정수 배열을 씁니다.</summary>
    public Task<PlcResult> WriteUInt16Async(string address, ushort[] values, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.UInt16, PlcValueConverter.GetBytes(values, _byteOrder), cancellationToken);
    }

    /// <summary>32비트 부호 있는 정수 한 개를 읽습니다.</summary>
    public Task<PlcResult<int>> ReadInt32Async(string address, CancellationToken cancellationToken = default)
    {
        return ReadScalarAsync(address, PlcDataType.Int32, PlcValueConverter.ToInt32, cancellationToken);
    }

    /// <summary>연속된 32비트 부호 있는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<int[]>> ReadInt32Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadArrayAsync(address, PlcDataType.Int32, count, PlcValueConverter.ToInt32Array, cancellationToken);
    }

    /// <summary>32비트 부호 있는 정수 한 개를 씁니다.</summary>
    public Task<PlcResult> WriteInt32Async(string address, int value, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Int32, PlcValueConverter.GetBytes(value, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 32비트 부호 있는 정수 배열을 씁니다.</summary>
    public Task<PlcResult> WriteInt32Async(string address, int[] values, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Int32, PlcValueConverter.GetBytes(values, _byteOrder), cancellationToken);
    }

    /// <summary>32비트 부호 없는 정수 한 개를 읽습니다.</summary>
    public Task<PlcResult<uint>> ReadUInt32Async(string address, CancellationToken cancellationToken = default)
    {
        return ReadScalarAsync(address, PlcDataType.UInt32, PlcValueConverter.ToUInt32, cancellationToken);
    }

    /// <summary>연속된 32비트 부호 없는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<uint[]>> ReadUInt32Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadArrayAsync(address, PlcDataType.UInt32, count, PlcValueConverter.ToUInt32Array, cancellationToken);
    }

    /// <summary>32비트 부호 없는 정수 한 개를 씁니다.</summary>
    public Task<PlcResult> WriteUInt32Async(string address, uint value, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.UInt32, PlcValueConverter.GetBytes(value, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 32비트 부호 없는 정수 배열을 씁니다.</summary>
    public Task<PlcResult> WriteUInt32Async(string address, uint[] values, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.UInt32, PlcValueConverter.GetBytes(values, _byteOrder), cancellationToken);
    }

    /// <summary>64비트 부호 있는 정수 한 개를 읽습니다.</summary>
    public Task<PlcResult<long>> ReadInt64Async(string address, CancellationToken cancellationToken = default)
    {
        return ReadScalarAsync(address, PlcDataType.Int64, PlcValueConverter.ToInt64, cancellationToken);
    }

    /// <summary>연속된 64비트 부호 있는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<long[]>> ReadInt64Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadArrayAsync(address, PlcDataType.Int64, count, PlcValueConverter.ToInt64Array, cancellationToken);
    }

    /// <summary>64비트 부호 있는 정수 한 개를 씁니다.</summary>
    public Task<PlcResult> WriteInt64Async(string address, long value, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Int64, PlcValueConverter.GetBytes(value, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 64비트 부호 있는 정수 배열을 씁니다.</summary>
    public Task<PlcResult> WriteInt64Async(string address, long[] values, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Int64, PlcValueConverter.GetBytes(values, _byteOrder), cancellationToken);
    }

    /// <summary>64비트 부호 없는 정수 한 개를 읽습니다.</summary>
    public Task<PlcResult<ulong>> ReadUInt64Async(string address, CancellationToken cancellationToken = default)
    {
        return ReadScalarAsync(address, PlcDataType.UInt64, PlcValueConverter.ToUInt64, cancellationToken);
    }

    /// <summary>연속된 64비트 부호 없는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<ulong[]>> ReadUInt64Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadArrayAsync(address, PlcDataType.UInt64, count, PlcValueConverter.ToUInt64Array, cancellationToken);
    }

    /// <summary>64비트 부호 없는 정수 한 개를 씁니다.</summary>
    public Task<PlcResult> WriteUInt64Async(string address, ulong value, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.UInt64, PlcValueConverter.GetBytes(value, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 64비트 부호 없는 정수 배열을 씁니다.</summary>
    public Task<PlcResult> WriteUInt64Async(string address, ulong[] values, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.UInt64, PlcValueConverter.GetBytes(values, _byteOrder), cancellationToken);
    }

    /// <summary>단정밀도 부동소수점 한 개를 읽습니다.</summary>
    public Task<PlcResult<float>> ReadSingleAsync(string address, CancellationToken cancellationToken = default)
    {
        return ReadScalarAsync(address, PlcDataType.Single, PlcValueConverter.ToSingle, cancellationToken);
    }

    /// <summary>연속된 단정밀도 부동소수점 배열을 읽습니다.</summary>
    public Task<PlcResult<float[]>> ReadSingleAsync(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadArrayAsync(address, PlcDataType.Single, count, PlcValueConverter.ToSingleArray, cancellationToken);
    }

    /// <summary>단정밀도 부동소수점 한 개를 씁니다.</summary>
    public Task<PlcResult> WriteSingleAsync(string address, float value, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Single, PlcValueConverter.GetBytes(value, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 단정밀도 부동소수점 배열을 씁니다.</summary>
    public Task<PlcResult> WriteSingleAsync(string address, float[] values, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Single, PlcValueConverter.GetBytes(values, _byteOrder), cancellationToken);
    }

    /// <summary>배정밀도 부동소수점 한 개를 읽습니다.</summary>
    public Task<PlcResult<double>> ReadDoubleAsync(string address, CancellationToken cancellationToken = default)
    {
        return ReadScalarAsync(address, PlcDataType.Double, PlcValueConverter.ToDouble, cancellationToken);
    }

    /// <summary>연속된 배정밀도 부동소수점 배열을 읽습니다.</summary>
    public Task<PlcResult<double[]>> ReadDoubleAsync(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadArrayAsync(address, PlcDataType.Double, count, PlcValueConverter.ToDoubleArray, cancellationToken);
    }

    /// <summary>배정밀도 부동소수점 한 개를 씁니다.</summary>
    public Task<PlcResult> WriteDoubleAsync(string address, double value, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Double, PlcValueConverter.GetBytes(value, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 배정밀도 부동소수점 배열을 씁니다.</summary>
    public Task<PlcResult> WriteDoubleAsync(string address, double[] values, CancellationToken cancellationToken = default)
    {
        return WriteCoreAsync(address, PlcDataType.Double, PlcValueConverter.GetBytes(values, _byteOrder), cancellationToken);
    }
}
