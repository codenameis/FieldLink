using FieldLink.PlcDrivers.Modbus.Clients;
using FieldLink.Communication;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Delta;
using System;
using FieldLink.PlcDrivers.Modbus;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Delta.Clients
{
    /// <summary>Delta RTU·ASCII를 교환하며 DVP의 불연속 주소 경계에서 요청을 나눕니다.</summary>
    /// <remarks>분할 구간마다 포트를 독점합니다. 앞 구간이 실패하면 뒤 구간을 보내지 않습니다.</remarks>
    public sealed class DeltaSerialClient : ModbusSerialClient
    {
        /// <summary>전송 외피와 DVP·AS 시리즈를 지정합니다.</summary>
        public DeltaSerialClient(ISerialClient transport, DeltaSeries series = DeltaSeries.Dvp,
            ModbusSerialEncoding encoding = ModbusSerialEncoding.Rtu, byte station = 1, TimeSpan? timeout = null)
            : base(transport, encoding, station, timeout: timeout, addressMapping: (address, function) => DeltaAddressParser.TranslateToModbusAddress(series, address, function))
        {
            if (!Enum.IsDefined(typeof(DeltaSeries), series))
                throw new ArgumentOutOfRangeException(nameof(series));
            Series = series;
        }
        /// <summary>주소 변환과 경계 분할에 사용하는 시리즈입니다.</summary>
        public DeltaSeries Series { get; }
        /// <inheritdoc/>
        public override Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken)) =>
            Series == DeltaSeries.Dvp ? ReadSegmentsAsync(DeltaDvpRequestPlanner.SplitReadWords(address, length),
                (a, n) => base.ReadAsync(a, n, cancellationToken), cancellationToken) : base.ReadAsync(address, length, cancellationToken);
        /// <inheritdoc/>
        public override Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken)) =>
            Series == DeltaSeries.Dvp ? ReadSegmentsAsync(DeltaDvpRequestPlanner.SplitReadBits(address, length),
                (a, n) => base.ReadBoolAsync(a, n, cancellationToken), cancellationToken) : base.ReadBoolAsync(address, length, cancellationToken);
        /// <inheritdoc/>
        public override Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (Series != DeltaSeries.Dvp)
                return base.WriteAsync(address, value, cancellationToken);
            var segments = DeltaDvpRequestPlanner.SplitWriteWords(address, value.Length);
            if (value.Length == 0 || value.Length % 2 != 0 || segments.Any(s => s.Length > 246))
                throw new ArgumentOutOfRangeException(nameof(value));
            return WriteSegmentsAsync(segments, value, (a, v) => base.WriteAsync(a, v, cancellationToken), cancellationToken);
        }
        /// <inheritdoc/>
        public override Task<OperationResult> WriteAsync(string address, bool[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (Series != DeltaSeries.Dvp)
                return base.WriteAsync(address, value, cancellationToken);
            var segments = DeltaDvpRequestPlanner.SplitWriteBits(address, value.Length);
            if (value.Length == 0 || segments.Any(s => s.Length > 1968))
                throw new ArgumentOutOfRangeException(nameof(value));
            return WriteSegmentsAsync(segments, value, (a, v) => base.WriteAsync(a, v, cancellationToken), cancellationToken);
        }
        private static async Task<OperationResult<T[]>> ReadSegmentsAsync<T>(DeltaRequestSegment[] segments,
            Func<string, ushort, Task<OperationResult<T[]>>> read, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var result = new List<T>();
            foreach (var segment in segments)
            {
                var response = await read(segment.Address, checked((ushort)segment.Length)).ConfigureAwait(false);
                if (!response.IsSuccess)
                    return response;
                result.AddRange(response.Content);
            }
            return OperationResult.CreateSuccessResult(result.ToArray());
        }
        private static async Task<OperationResult> WriteSegmentsAsync<T>(DeltaRequestSegment[] segments, T[] values,
            Func<string, T[], Task<OperationResult>> write, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            foreach (var segment in segments)
            {
                var response = await write(segment.Address, values.SelectMiddle(segment.Offset, segment.Length)).ConfigureAwait(false);
                if (!response.IsSuccess)
                    return response;
            }
            return OperationResult.CreateSuccessResult();
        }
    }
}
