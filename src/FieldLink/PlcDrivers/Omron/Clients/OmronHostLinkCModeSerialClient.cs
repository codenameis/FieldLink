using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Omron;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Omron.Clients
{
    /// <summary>Omron Host Link C 모드의 워드 읽기·쓰기와 PLC 모드 제어를 교환합니다.</summary>
    public sealed class OmronHostLinkCModeSerialClient
    {
        private readonly SerialProtocolExchange exchange;
        /// <summary>호출자가 소유한 포트와 기본 유닛 번호를 지정합니다.</summary>
        public OmronHostLinkCModeSerialClient(ISerialClient transport, byte unitNumber = 0, TimeSpan? timeout = null)
        { UnitNumber = unitNumber; exchange = new SerialProtocolExchange(transport, new DelimitedFrame(new byte[] { 13 }), timeout); }
        /// <summary>주소에서 재정의하지 않은 유닛 번호입니다.</summary>
        public byte UnitNumber { get; }
        /// <summary>30워드 단위로 읽고 데이터를 연결합니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte unit = UnitFor(ref address);
            return exchange.ExecuteSequenceAsync(Pack(OmronHostLinkCModeCommandBuilder.BuildReadCommand(address, length, false), unit),
                (i, response) => OmronHostLinkCModeResponseParser.ResponseValidAnalysis(response, true), cancellationToken);
        }
        /// <summary>30워드 단위로 기록하며 첫 장치 오류에서 중단합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte unit = UnitFor(ref address);
            return await exchange.ExecuteSequenceAsync(Pack(OmronHostLinkCModeCommandBuilder.BuildWriteWordCommand(address, value), unit),
                (i, response) => OmronHostLinkCModeResponseParser.ResponseValidAnalysis(response, false), cancellationToken).ConfigureAwait(false);
        }
        /// <summary>PLC 모델명을 조회합니다.</summary>
        public Task<OperationResult<string>> ReadPlcTypeAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(OperationResult.CreateSuccessResult(OmronHostLinkCModeControlCommandBuilder.BuildReadPlcType(UnitNumber)),
                response => OmronHostLinkCModeControlResponseParser.ParseReadPlcType(response, UnitNumber), cancellationToken);
        /// <summary>PLC 운전 모드를 조회합니다.</summary>
        public Task<OperationResult<int>> ReadPlcModeAsync(CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(OperationResult.CreateSuccessResult(OmronHostLinkCModeControlCommandBuilder.BuildReadPlcMode(UnitNumber)),
                response => OmronHostLinkCModeControlResponseParser.ParseReadPlcMode(response, UnitNumber), cancellationToken);
        /// <summary>PLC 운전 모드를 변경합니다.</summary>
        public Task<OperationResult> ChangePlcModeAsync(byte mode, CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(OperationResult.CreateSuccessResult(OmronHostLinkCModeControlCommandBuilder.BuildChangePlcMode(UnitNumber, mode)),
                response => OmronHostLinkCModeControlResponseParser.ParseChangePlcMode(response, UnitNumber, mode), cancellationToken);
        private byte UnitFor(ref string address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            return (byte)AddressParameters.ExtractParameter(ref address, "s", UnitNumber);
        }
        private static OperationResult<List<byte[]>> Pack(OperationResult<List<byte[]>> commands, byte unit) => commands.IsSuccess
            ? OperationResult.CreateSuccessResult(commands.Content.Select(c => OmronHostLinkCModeCommandBuilder.PackCommand(c, unit)).ToList()) : commands;
    }
}
