using System.Threading;
using System.Threading.Tasks;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers
{
    public sealed partial class PlcClient
    {
        /// <summary>short 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<short>> ReadInt16Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadInt16Async(address, 1, cancellationToken));
        /// <summary>short 값을 count개 연속 읽습니다. count는 자료형 원소 수입니다.</summary>
        public Task<OperationResult<short[]>> ReadInt16Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, PlcValueType.Int16, converter.ReadInt16, cancellationToken);
        /// <summary>short 값 한 개를 기록하고 장치 응답을 확인합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, short value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValueAsync(address, 1, PlcValueType.Int16, () => converter.GetBytes(value), cancellationToken);
        /// <summary>short 배열을 연속 기록합니다. 첫 비동기 대기 전에 입력을 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, short[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, PlcValueType.Int16, converter.GetBytes, cancellationToken);

        /// <summary>ushort 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<ushort>> ReadUInt16Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadUInt16Async(address, 1, cancellationToken));
        /// <summary>ushort 값을 count개 연속 읽습니다. count는 자료형 원소 수입니다.</summary>
        public Task<OperationResult<ushort[]>> ReadUInt16Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, PlcValueType.UInt16, converter.ReadUInt16, cancellationToken);
        /// <summary>ushort 값 한 개를 기록하고 장치 응답을 확인합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, ushort value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValueAsync(address, 1, PlcValueType.UInt16, () => converter.GetBytes(value), cancellationToken);
        /// <summary>ushort 배열을 연속 기록합니다. 첫 비동기 대기 전에 입력을 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, ushort[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, PlcValueType.UInt16, converter.GetBytes, cancellationToken);

        /// <summary>int 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<int>> ReadInt32Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadInt32Async(address, 1, cancellationToken));
        /// <summary>int 값을 count개 연속 읽습니다. count는 자료형 원소 수입니다.</summary>
        public Task<OperationResult<int[]>> ReadInt32Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, PlcValueType.Int32, converter.ReadInt32, cancellationToken);
        /// <summary>int 값 한 개를 기록하고 장치 응답을 확인합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, int value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValueAsync(address, 1, PlcValueType.Int32, () => converter.GetBytes(value), cancellationToken);
        /// <summary>int 배열을 연속 기록합니다. 첫 비동기 대기 전에 입력을 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, int[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, PlcValueType.Int32, converter.GetBytes, cancellationToken);

        /// <summary>uint 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<uint>> ReadUInt32Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadUInt32Async(address, 1, cancellationToken));
        /// <summary>uint 값을 count개 연속 읽습니다. count는 자료형 원소 수입니다.</summary>
        public Task<OperationResult<uint[]>> ReadUInt32Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, PlcValueType.UInt32, converter.ReadUInt32, cancellationToken);
        /// <summary>uint 값 한 개를 기록하고 장치 응답을 확인합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, uint value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValueAsync(address, 1, PlcValueType.UInt32, () => converter.GetBytes(value), cancellationToken);
        /// <summary>uint 배열을 연속 기록합니다. 첫 비동기 대기 전에 입력을 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, uint[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, PlcValueType.UInt32, converter.GetBytes, cancellationToken);

        /// <summary>long 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<long>> ReadInt64Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadInt64Async(address, 1, cancellationToken));
        /// <summary>long 값을 count개 연속 읽습니다. count는 자료형 원소 수입니다.</summary>
        public Task<OperationResult<long[]>> ReadInt64Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, PlcValueType.Int64, converter.ReadInt64, cancellationToken);
        /// <summary>long 값 한 개를 기록하고 장치 응답을 확인합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, long value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValueAsync(address, 1, PlcValueType.Int64, () => converter.GetBytes(value), cancellationToken);
        /// <summary>long 배열을 연속 기록합니다. 첫 비동기 대기 전에 입력을 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, long[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, PlcValueType.Int64, converter.GetBytes, cancellationToken);

        /// <summary>ulong 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<ulong>> ReadUInt64Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadUInt64Async(address, 1, cancellationToken));
        /// <summary>ulong 값을 count개 연속 읽습니다. count는 자료형 원소 수입니다.</summary>
        public Task<OperationResult<ulong[]>> ReadUInt64Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, PlcValueType.UInt64, converter.ReadUInt64, cancellationToken);
        /// <summary>ulong 값 한 개를 기록하고 장치 응답을 확인합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, ulong value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValueAsync(address, 1, PlcValueType.UInt64, () => converter.GetBytes(value), cancellationToken);
        /// <summary>ulong 배열을 연속 기록합니다. 첫 비동기 대기 전에 입력을 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, ulong[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, PlcValueType.UInt64, converter.GetBytes, cancellationToken);

        /// <summary>float 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<float>> ReadFloatAsync(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadFloatAsync(address, 1, cancellationToken));
        /// <summary>float 값을 count개 연속 읽습니다. count는 자료형 원소 수입니다.</summary>
        public Task<OperationResult<float[]>> ReadFloatAsync(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, PlcValueType.Float, converter.ReadSingle, cancellationToken);
        /// <summary>float 값 한 개를 기록하고 장치 응답을 확인합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, float value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValueAsync(address, 1, PlcValueType.Float, () => converter.GetBytes(value), cancellationToken);
        /// <summary>float 배열을 연속 기록합니다. 첫 비동기 대기 전에 입력을 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, float[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, PlcValueType.Float, converter.GetBytes, cancellationToken);

        /// <summary>double 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<double>> ReadDoubleAsync(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadDoubleAsync(address, 1, cancellationToken));
        /// <summary>double 값을 count개 연속 읽습니다. count는 자료형 원소 수입니다.</summary>
        public Task<OperationResult<double[]>> ReadDoubleAsync(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, PlcValueType.Double, converter.ReadDouble, cancellationToken);
        /// <summary>double 값 한 개를 기록하고 장치 응답을 확인합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, double value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValueAsync(address, 1, PlcValueType.Double, () => converter.GetBytes(value), cancellationToken);
        /// <summary>double 배열을 연속 기록합니다. 첫 비동기 대기 전에 입력을 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, double[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, PlcValueType.Double, converter.GetBytes, cancellationToken);

    }
}
