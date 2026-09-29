using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.AllenBradley;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.AllenBradley.Clients
{
    /// <summary>DF1의 논리 주소 읽기·쓰기를 수행합니다. 길이는 바이트 단위이며 포트는 호출자가 관리합니다.</summary>
    public sealed class Df1SerialClient
    {
        private readonly SerialProtocolExchange exchange;
        private readonly Df1FrameOptions options;
        private readonly byte station, destination, source;
        private int sequence = -1;
        /// <summary>전송, 노드 번호, BCC 또는 CRC16 검증 방식을 지정합니다.</summary>
        public Df1SerialClient(ISerialClient transport, byte station = 0, byte destination = 0, byte source = 0,
            CheckType checkType = CheckType.CRC16, TimeSpan? timeout = null)
        {
            if (checkType != CheckType.CRC16 && checkType != CheckType.BCC)
                throw new ArgumentOutOfRangeException(nameof(checkType));
            this.station = station; this.destination = destination; this.source = source;
            options = new Df1FrameOptions { CheckType = checkType };
            exchange = new SerialProtocolExchange(transport, new EscapedTerminatorFrame(0x10, 3, checkType == CheckType.BCC ? 1 : 2), timeout);
        }
        /// <summary>N7:0 등의 주소를 읽습니다. s=, dst=, src= 접두사를 지원합니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken token = default(CancellationToken)) =>
            ExecuteAsync(address, (dst, src, tns, addr) => AllenBradleyDF1SerialCommandBuilder.BuildProtectedTypedLogicalReadWithThreeAddressFields(dst, src, tns, addr, length), token);
        /// <summary>논리 주소에 바이트 데이터를 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken token = default(CancellationToken)) =>
            await ExecuteAsync(address, (dst, src, tns, addr) => AllenBradleyDF1SerialCommandBuilder.BuildProtectedTypedLogicalWriteWithThreeAddressFields(dst, src, tns, addr, value), token).ConfigureAwait(false);
        private Task<OperationResult<byte[]>> ExecuteAsync(string address, Func<byte, byte, int, string, OperationResult<byte[]>> build, CancellationToken token)
        {
            byte stat = (byte)AddressParameters.ExtractParameter(ref address, "s", station);
            byte dst = (byte)AddressParameters.ExtractParameter(ref address, "dst", destination);
            byte src = (byte)AddressParameters.ExtractParameter(ref address, "src", source);
            var command = build(dst, src, unchecked((ushort)Interlocked.Increment(ref sequence)), address);
            if (command.IsSuccess)
                command = OperationResult.CreateSuccessResult(AllenBradleyDF1SerialCommandBuilder.PackCommand(options, stat, command.Content));
            return exchange.ExecuteAsync(command, AllenBradleyDF1SerialResponseParser.ExtractActualData, token);
        }
    }
}
