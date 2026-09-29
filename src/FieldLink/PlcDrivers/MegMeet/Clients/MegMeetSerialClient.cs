using FieldLink.PlcDrivers.Modbus.Clients;
using FieldLink.Communication;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.MegMeet;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.MegMeet.Clients
{
    /// <summary>MegMeet 주소를 RTU로 교환하며 불연속 영역의 읽기를 분할합니다.</summary>
    public sealed class MegMeetSerialClient : ModbusSerialClient
    {
        /// <summary>호출자가 소유한 포트와 기본 국번을 지정합니다.</summary>
        public MegMeetSerialClient(ISerialClient transport, byte station = 1, TimeSpan? timeout = null)
            : base(transport, station: station, timeout: timeout, addressMapping: MegMeetAddressParser.PraseMegMeetAddress) { }
        /// <inheritdoc/>
        public override Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadSplitAsync(address, length, MegMeetAddressParser.GetWordCuttingAddress(),
                (a, n) => base.ReadAsync(a, n, cancellationToken), cancellationToken);
        /// <inheritdoc/>
        public override Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken)) =>
            ReadSplitAsync(address, length, MegMeetAddressParser.GetBoolCuttingAddress(),
                (a, n) => base.ReadBoolAsync(a, n, cancellationToken), cancellationToken);

        private static async Task<OperationResult<T[]>> ReadSplitAsync<T>(string address, ushort length, List<CuttingAddress> boundaries,
            Func<string, ushort, Task<OperationResult<T[]>>> read, CancellationToken token)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            token.ThrowIfCancellationRequested();
            string original = address;
            var station = AddressParameters.ExtractParameter(ref address, "s");
            foreach (var boundary in boundaries)
            {
                if (!address.StartsWith(boundary.DataType, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!int.TryParse(address.Substring(boundary.DataType.Length), out int start))
                    continue;
                if (start >= boundary.Address || start + length <= boundary.Address)
                    continue;
                ushort firstLength = (ushort)(boundary.Address - start);
                var first = await read(original, firstLength).ConfigureAwait(false);
                if (!first.IsSuccess)
                    return first;
                string secondAddress = (station.IsSuccess ? "s=" + station.Content + ";" : "") + boundary.DataType + boundary.Address;
                var second = await read(secondAddress, (ushort)(length - firstLength)).ConfigureAwait(false);
                if (!second.IsSuccess)
                    return second;
                return OperationResult.CreateSuccessResult(ProtocolBytes.SpliceArray(first.Content, second.Content));
            }
            return await read(original, length).ConfigureAwait(false);
        }
    }
}
