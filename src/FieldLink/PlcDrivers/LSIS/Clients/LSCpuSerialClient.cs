using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.LSIS;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.LSIS.Clients
{
    /// <summary>LS LSCpu 시리얼의 바이트·비트 데이터를 교환합니다.</summary>
    public sealed class LSCpuSerialClient
    {
        private readonly SerialProtocolExchange exchange;
        /// <summary>호출자가 소유한 포트와 국번을 지정합니다. ETX까지 수신합니다.</summary>
        public LSCpuSerialClient(ISerialClient transport, byte station = 5, TimeSpan? timeout = null)
        { Station = station; exchange = new SerialProtocolExchange(transport, new DelimitedFrame(new byte[] { 3 }), timeout); }
        /// <summary>주소에서 재정의하지 않은 기본 국번입니다.</summary>
        public byte Station { get; }
        /// <summary>바이트 단위 길이로 데이터를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return ExecuteAsync(LSCpuCommandBuilder.BuildReadCommand(station, address, length), cancellationToken);
        }
        /// <summary>첫 바이트를 읽습니다.</summary>
        public async Task<OperationResult<byte>> ReadByteAsync(string address, CancellationToken cancellationToken = default(CancellationToken))
        {
            var read = await ReadAsync(address, 1, cancellationToken).ConfigureAwait(false);
            if (!read.IsSuccess)
                return read.ConvertFailed<byte>();
            return read.Content.Length == 0 ? new OperationResult<byte>("바이트 응답이 비어 있습니다.") : OperationResult.CreateSuccessResult(read.Content[0]);
        }
        /// <summary>점 주소의 비트 위치를 포함하여 필요한 바이트를 읽습니다.</summary>
        public async Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            int bit = AddressParameters.GetBitIndexInformation(ref address);
            var read = await ReadAsync(address, checked((ushort)AddressParameters.CalculateOccupyLength(bit, length)), cancellationToken).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(read.Content.ToBoolArray().SelectMiddle(bit, length)) : read.ConvertFailed<bool[]>();
        }
        /// <summary>바이트 데이터를 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return await ExecuteAsync(LSCpuCommandBuilder.BuildWriteCommand(station, address, value), cancellationToken).ConfigureAwait(false);
        }
        /// <summary>바이트 하나를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteAsync(address, new byte[] { value }, cancellationToken);
        /// <summary>비트 하나를 전용 명령으로 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, bool value, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            var parsed = LSCpuAddressParser.AnalysisAddress(address, true);
            if (!parsed.IsSuccess)
                return parsed;
            return await ExecuteAsync(LSCpuCommandBuilder.BuildWriteOneCommand(station, parsed.Content,
                new byte[] { value ? (byte)1 : (byte)0 }), cancellationToken).ConfigureAwait(false);
        }

        private Task<OperationResult<byte[]>> ExecuteAsync(OperationResult<byte[]> command, CancellationToken token) =>
            exchange.ExecuteAsync(command, response => LSCpuResponseParser.UnpackResponseContent(command.Content, response), token);
        private byte StationFor(ref string address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            return (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
        }
    }
}
