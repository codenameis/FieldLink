using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.FATEK;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.FATEK.Clients
{
    /// <summary>FATEK 프로그래밍 포트의 읽기·쓰기와 운전 제어를 교환합니다.</summary>
    public sealed class FatekProgramSerialClient
    {
        private readonly SerialProtocolExchange exchange;
        /// <summary>호출자가 소유한 포트와 기본 국번을 지정합니다.</summary>
        public FatekProgramSerialClient(ISerialClient transport, byte station = 1, TimeSpan? timeout = null)
        { Station = station; exchange = new SerialProtocolExchange(transport, new DelimitedFrame(new byte[] { 3 }), timeout); }
        /// <summary>주소에서 재정의하지 않은 기본 국번입니다.</summary>
        public byte Station { get; }
        /// <summary>64워드 단위로 읽고 순서대로 연결합니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteSequenceAsync(FatekProgramCommandBuilder.BuildReadWordCommand(Station, address, length),
                (i, response) => FatekProgramReadResponseParser.ParseWords(response, (ushort)Math.Min(64, length - i * 64)), cancellationToken);
        /// <summary>255비트 단위로 읽고 순서대로 연결합니다.</summary>
        public Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteSequenceAsync(FatekProgramCommandBuilder.BuildReadBoolCommand(Station, address, length),
                (i, response) => FatekProgramReadResponseParser.ParseBits(response, (ushort)Math.Min(255, length - i * 255)), cancellationToken);
        /// <summary>워드 배열을 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(FatekProgramCommandBuilder.BuildWriteByteCommand(Station, address, value), FatekProgramResponseParser.CheckResponse, cancellationToken);
        /// <summary>비트 배열을 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool[] value, CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(FatekProgramCommandBuilder.BuildWriteBoolCommand(Station, address, value), FatekProgramResponseParser.CheckResponse, cancellationToken);
        /// <summary>PLC 운전을 시작합니다.</summary>
        public Task<OperationResult> RunAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(OperationResult.CreateSuccessResult(FatekControlCommandBuilder.BuildRun(Station)), FatekProgramResponseParser.CheckResponse, cancellationToken);
        /// <summary>PLC 운전을 정지합니다.</summary>
        public Task<OperationResult> StopAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(OperationResult.CreateSuccessResult(FatekControlCommandBuilder.BuildStop(Station)), FatekProgramResponseParser.CheckResponse, cancellationToken);
        /// <summary>PLC 상태 비트를 읽습니다.</summary>
        public Task<OperationResult<bool[]>> ReadStatusAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(OperationResult.CreateSuccessResult(FatekProgramControlCommandBuilder.BuildReadStatus(Station)),
                response => FatekProgramControlResponseParser.ParseReadStatus(response, Station), cancellationToken);
    }
}
