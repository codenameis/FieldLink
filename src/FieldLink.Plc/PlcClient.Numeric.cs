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
        return ReadCoreAsync(address, PlcDataType.Int16, 1, bytes => PlcValueConverter.ToInt16(bytes, 0, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 16비트 부호 있는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<short[]>> ReadInt16Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.Int16, count, bytes => PlcValueConverter.ToInt16Array(bytes, 0, count, _byteOrder), cancellationToken);
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
        return ReadCoreAsync(address, PlcDataType.UInt16, 1, bytes => PlcValueConverter.ToUInt16(bytes, 0, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 16비트 부호 없는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<ushort[]>> ReadUInt16Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.UInt16, count, bytes => PlcValueConverter.ToUInt16Array(bytes, 0, count, _byteOrder), cancellationToken);
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
        return ReadCoreAsync(address, PlcDataType.Int32, 1, bytes => PlcValueConverter.ToInt32(bytes, 0, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 32비트 부호 있는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<int[]>> ReadInt32Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.Int32, count, bytes => PlcValueConverter.ToInt32Array(bytes, 0, count, _byteOrder), cancellationToken);
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
        return ReadCoreAsync(address, PlcDataType.UInt32, 1, bytes => PlcValueConverter.ToUInt32(bytes, 0, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 32비트 부호 없는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<uint[]>> ReadUInt32Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.UInt32, count, bytes => PlcValueConverter.ToUInt32Array(bytes, 0, count, _byteOrder), cancellationToken);
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
        return ReadCoreAsync(address, PlcDataType.Int64, 1, bytes => PlcValueConverter.ToInt64(bytes, 0, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 64비트 부호 있는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<long[]>> ReadInt64Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.Int64, count, bytes => PlcValueConverter.ToInt64Array(bytes, 0, count, _byteOrder), cancellationToken);
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
        return ReadCoreAsync(address, PlcDataType.UInt64, 1, bytes => PlcValueConverter.ToUInt64(bytes, 0, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 64비트 부호 없는 정수 배열을 읽습니다.</summary>
    public Task<PlcResult<ulong[]>> ReadUInt64Async(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.UInt64, count, bytes => PlcValueConverter.ToUInt64Array(bytes, 0, count, _byteOrder), cancellationToken);
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
        return ReadCoreAsync(address, PlcDataType.Single, 1, bytes => PlcValueConverter.ToSingle(bytes, 0, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 단정밀도 부동소수점 배열을 읽습니다.</summary>
    public Task<PlcResult<float[]>> ReadSingleAsync(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.Single, count, bytes => PlcValueConverter.ToSingleArray(bytes, 0, count, _byteOrder), cancellationToken);
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
        return ReadCoreAsync(address, PlcDataType.Double, 1, bytes => PlcValueConverter.ToDouble(bytes, 0, _byteOrder), cancellationToken);
    }

    /// <summary>연속된 배정밀도 부동소수점 배열을 읽습니다.</summary>
    public Task<PlcResult<double[]>> ReadDoubleAsync(string address, int count, CancellationToken cancellationToken = default)
    {
        return ReadCoreAsync(address, PlcDataType.Double, count, bytes => PlcValueConverter.ToDoubleArray(bytes, 0, count, _byteOrder), cancellationToken);
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
