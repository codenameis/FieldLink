using System.Threading;
using System.Threading.Tasks;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers.Melsec.Clients
{
    public sealed partial class MelsecMcClient
    {
        /// <summary>16비트 short 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<short>> ReadInt16Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadInt16Async(address, 1, cancellationToken));
        /// <summary>short 값을 count개 연속 읽습니다. count는 워드 수가 아닌 원소 수입니다.</summary>
        public Task<OperationResult<short[]>> ReadInt16Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, 2, converter.ReadInt16, cancellationToken);
        /// <summary>short 값 한 개를 1워드에 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, short value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteBytesAsync(address, 2, () => converter.GetBytes(value), cancellationToken);
        /// <summary>short 배열을 연속 기록합니다. 입력값은 호출 시 바이트로 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, short[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, 2, converter.GetBytes, cancellationToken);

        /// <summary>16비트 ushort 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<ushort>> ReadUInt16Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadUInt16Async(address, 1, cancellationToken));
        /// <summary>ushort 값을 count개 연속 읽습니다. count는 워드 수가 아닌 원소 수입니다.</summary>
        public Task<OperationResult<ushort[]>> ReadUInt16Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, 2, converter.ReadUInt16, cancellationToken);
        /// <summary>ushort 값 한 개를 1워드에 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, ushort value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteBytesAsync(address, 2, () => converter.GetBytes(value), cancellationToken);
        /// <summary>ushort 배열을 연속 기록합니다. 입력값은 호출 시 바이트로 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, ushort[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, 2, converter.GetBytes, cancellationToken);

        /// <summary>32비트 int 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<int>> ReadInt32Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadInt32Async(address, 1, cancellationToken));
        /// <summary>int 값을 count개 연속 읽습니다. count는 워드 수가 아닌 원소 수입니다.</summary>
        public Task<OperationResult<int[]>> ReadInt32Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, 4, converter.ReadInt32, cancellationToken);
        /// <summary>int 값 한 개를 2워드에 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, int value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteBytesAsync(address, 4, () => converter.GetBytes(value), cancellationToken);
        /// <summary>int 배열을 연속 기록합니다. 입력값은 호출 시 바이트로 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, int[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, 4, converter.GetBytes, cancellationToken);

        /// <summary>32비트 uint 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<uint>> ReadUInt32Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadUInt32Async(address, 1, cancellationToken));
        /// <summary>uint 값을 count개 연속 읽습니다. count는 워드 수가 아닌 원소 수입니다.</summary>
        public Task<OperationResult<uint[]>> ReadUInt32Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, 4, converter.ReadUInt32, cancellationToken);
        /// <summary>uint 값 한 개를 2워드에 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, uint value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteBytesAsync(address, 4, () => converter.GetBytes(value), cancellationToken);
        /// <summary>uint 배열을 연속 기록합니다. 입력값은 호출 시 바이트로 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, uint[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, 4, converter.GetBytes, cancellationToken);

        /// <summary>64비트 long 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<long>> ReadInt64Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadInt64Async(address, 1, cancellationToken));
        /// <summary>long 값을 count개 연속 읽습니다. count는 워드 수가 아닌 원소 수입니다.</summary>
        public Task<OperationResult<long[]>> ReadInt64Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, 8, converter.ReadInt64, cancellationToken);
        /// <summary>long 값 한 개를 4워드에 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, long value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteBytesAsync(address, 8, () => converter.GetBytes(value), cancellationToken);
        /// <summary>long 배열을 연속 기록합니다. 입력값은 호출 시 바이트로 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, long[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, 8, converter.GetBytes, cancellationToken);

        /// <summary>64비트 ulong 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<ulong>> ReadUInt64Async(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadUInt64Async(address, 1, cancellationToken));
        /// <summary>ulong 값을 count개 연속 읽습니다. count는 워드 수가 아닌 원소 수입니다.</summary>
        public Task<OperationResult<ulong[]>> ReadUInt64Async(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, 8, converter.ReadUInt64, cancellationToken);
        /// <summary>ulong 값 한 개를 4워드에 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, ulong value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteBytesAsync(address, 8, () => converter.GetBytes(value), cancellationToken);
        /// <summary>ulong 배열을 연속 기록합니다. 입력값은 호출 시 바이트로 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, ulong[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, 8, converter.GetBytes, cancellationToken);

        /// <summary>32비트 float 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<float>> ReadFloatAsync(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadFloatAsync(address, 1, cancellationToken));
        /// <summary>float 값을 count개 연속 읽습니다. count는 워드 수가 아닌 원소 수입니다.</summary>
        public Task<OperationResult<float[]>> ReadFloatAsync(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, 4, converter.ReadSingle, cancellationToken);
        /// <summary>float 값 한 개를 2워드에 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, float value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteBytesAsync(address, 4, () => converter.GetBytes(value), cancellationToken);
        /// <summary>float 배열을 연속 기록합니다. 입력값은 호출 시 바이트로 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, float[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, 4, converter.GetBytes, cancellationToken);

        /// <summary>64비트 double 값 한 개를 읽습니다.</summary>
        public Task<OperationResult<double>> ReadDoubleAsync(string address, CancellationToken cancellationToken = default(CancellationToken)) =>
            FirstAsync(ReadDoubleAsync(address, 1, cancellationToken));
        /// <summary>double 값을 count개 연속 읽습니다. count는 워드 수가 아닌 원소 수입니다.</summary>
        public Task<OperationResult<double[]>> ReadDoubleAsync(string address, int count, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadValuesAsync(address, count, 8, converter.ReadDouble, cancellationToken);
        /// <summary>double 값 한 개를 4워드에 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, double value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteBytesAsync(address, 8, () => converter.GetBytes(value), cancellationToken);
        /// <summary>double 배열을 연속 기록합니다. 입력값은 호출 시 바이트로 복사합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, double[] values, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteValuesAsync(address, values, 8, converter.GetBytes, cancellationToken);

    }
}
