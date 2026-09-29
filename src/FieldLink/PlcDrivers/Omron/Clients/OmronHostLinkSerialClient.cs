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
    /// <summary>FINS 코어를 Omron Host Link 외피에 넣어 교환합니다.</summary>
    public sealed class OmronHostLinkSerialClient
    {
        private readonly SerialProtocolExchange exchange;
        private readonly HostLinkFrameOptions options;
        /// <summary>프레임 설정을 복사하여 사용합니다. 포트의 열기·종료는 호출자가 담당합니다.</summary>
        public OmronHostLinkSerialClient(ISerialClient transport, HostLinkFrameOptions options = null, TimeSpan? timeout = null)
        {
            options = options ?? new HostLinkFrameOptions();
            if (options.ReadSplits < 1 || options.ReadSplits > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(options));
            this.options = new HostLinkFrameOptions { ICF = options.ICF, DA2 = options.DA2, SA2 = options.SA2, SID = options.SID,
                ResponseWaitTime = options.ResponseWaitTime, UnitNumber = options.UnitNumber, ReadSplits = options.ReadSplits, PlcType = options.PlcType };
            exchange = new SerialProtocolExchange(transport, new DelimitedFrame(new byte[] { 13 }), timeout);
        }
        /// <summary>연속 워드 데이터를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = UnitFor(ref address);
            return ReadCommandsAsync(OmronFinsNetCommandBuilder.BuildReadCommand(options.PlcType, address, length, false, options.ReadSplits), station, cancellationToken);
        }
        /// <summary>이산 워드 주소 목록을 읽습니다. 첫 주소의 s 매개변수를 전체 요청에 적용합니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string[] address, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            var copy = (string[])address.Clone();
            byte station = copy.Length > 0 ? UnitFor(ref copy[0]) : options.UnitNumber;
            return ReadCommandsAsync(OmronFinsNetCommandBuilder.BuildReadCommand(copy, options.PlcType), station, cancellationToken);
        }
        /// <summary>연속 비트 데이터를 읽습니다.</summary>
        public async Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = UnitFor(ref address);
            var read = await ReadCommandsAsync(OmronFinsNetCommandBuilder.BuildReadCommand(options.PlcType, address, length, true), station, cancellationToken).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(read.Content.Select(b => b != 0).ToArray()) : read.ConvertFailed<bool[]>();
        }
        /// <summary>워드 데이터를 기록합니다. 정상 통신이어도 장치 오류 응답은 실패로 반환합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = UnitFor(ref address);
            return WriteCommandAsync(OmronFinsNetCommandBuilder.BuildWriteWordCommand(options.PlcType, address, value, false), station, cancellationToken);
        }
        /// <summary>비트 데이터를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            byte station = UnitFor(ref address);
            return WriteCommandAsync(OmronFinsNetCommandBuilder.BuildWriteWordCommand(options.PlcType, address, value.Select(b => b ? (byte)1 : (byte)0).ToArray(), true), station, cancellationToken);
        }
        private Task<OperationResult<byte[]>> ReadCommandsAsync(OperationResult<List<byte[]>> commands, byte station, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!commands.IsSuccess)
                return Task.FromResult(commands.ConvertFailed<byte[]>());
            var frames = commands.Content.Select(c => OmronHostLinkCommandBuilder.PackCommand(options, station, c)).ToList();
            return exchange.ExecuteSequenceAsync(OperationResult.CreateSuccessResult(frames),
                (i, response) => OmronHostLinkResponseParser.ResponseValidAnalysis(frames[i], response), token);
        }
        private async Task<OperationResult> WriteCommandAsync(OperationResult<byte[]> command, byte station, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!command.IsSuccess)
                return command;
            byte[] frame = OmronHostLinkCommandBuilder.PackCommand(options, station, command.Content);
            return await exchange.ExecuteAsync(OperationResult.CreateSuccessResult(frame),
                response => OmronHostLinkResponseParser.ResponseValidAnalysis(frame, response), token).ConfigureAwait(false);
        }
        private byte UnitFor(ref string address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            return (byte)AddressParameters.ExtractParameter(ref address, "s", options.UnitNumber);
        }
    }
}
