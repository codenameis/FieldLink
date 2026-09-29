using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Keyence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Keyence.Clients
{
    /// <summary>KV 상위 링크의 연결 협상과 명령 교환을 담당합니다. 포트의 수명은 호출자가 관리합니다.</summary>
    public sealed class KeyenceNanoSerialClient
    {
        private readonly SerialProtocolSession<Session> session;
        private readonly IFrameBoundary boundary = new DelimitedFrame(new byte[] { 13, 10 });
        private readonly TimeSpan timeout;
        private readonly byte station;
        private readonly bool useStation;

        /// <summary>전송과 국번 사용 여부를 지정합니다. 새 포트의 첫 명령 전에 CR 연결 협상을 수행합니다.</summary>
        public KeyenceNanoSerialClient(ISerialClient transport, byte station = 0, bool useStation = false, TimeSpan? timeout = null)
        {
            this.station = station;
            this.useStation = useStation;
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            session = new SerialProtocolSession<Session>(transport, tx => Task.FromResult(new Session()));
        }

        /// <summary>워드 또는 unit=n;주소 형식의 확장 메모리를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken token = default(CancellationToken))
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (address.StartsWith("unit=", StringComparison.Ordinal))
            {
                byte unit = (byte)AddressParameters.ExtractParameter(ref address, "unit", 0);
                if (!ushort.TryParse(address, out ushort offset))
                    return Task.FromResult(new OperationResult<byte[]>("확장 메모리 주소는 0~65535이어야 합니다."));
                return ReadExpansionMemoryAsync(unit, offset, length, token);
            }
            return ReadSequenceAsync(address, length, KeyenceNanoResponseParser.ExtractActualData, token);
        }

        /// <summary>연속 비트를 읽습니다.</summary>
        public Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken token = default(CancellationToken)) =>
            ReadSequenceAsync(address, length, KeyenceNanoResponseParser.ExtractActualBoolData, token);

        private Task<OperationResult<T[]>> ReadSequenceAsync<T>(string address, ushort length,
            Func<string, byte[], OperationResult<T[]>> parse, CancellationToken token)
        {
            var parsed = KeyenceNanoAddress.ParseFrom(address, length);
            if (!parsed.IsSuccess)
                return Task.FromResult(parsed.ConvertFailed<T[]>());
            var commands = KeyenceNanoCommandBuilder.BuildReadCommand(parsed.Content, length);
            if (!commands.IsSuccess)
                return Task.FromResult(commands.ConvertFailed<T[]>());
            return ExecuteAsync(async tx =>
            {
                var values = new List<T>();
                foreach (byte[] command in commands.Content)
                {
                    byte[] response = await tx.ExchangeAsync(command, boundary).ConfigureAwait(false);
                    var check = KeyenceNanoResponseParser.CheckPlcReadResponse(response);
                    if (!check.IsSuccess)
                        return check.ConvertFailed<T[]>();
                    var data = parse(parsed.Content.DataCode, response);
                    if (!data.IsSuccess)
                        return data;
                    values.AddRange(data.Content);
                }
                return OperationResult.CreateSuccessResult(values.ToArray());
            }, token);
        }

        /// <summary>워드 데이터를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken token = default(CancellationToken)) => WriteAsync(KeyenceNanoCommandBuilder.BuildWriteCommand(address, value), token);
        /// <summary>비트 하나를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool value, CancellationToken token = default(CancellationToken)) => WriteAsync(KeyenceNanoCommandBuilder.BuildWriteCommand(address, value), token);
        /// <summary>연속 비트를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool[] value, CancellationToken token = default(CancellationToken)) => WriteAsync(KeyenceNanoCommandBuilder.BuildWriteCommand(address, value), token);
        private Task<OperationResult> WriteAsync(OperationResult<byte[]> command, CancellationToken token) =>
            !command.IsSuccess ? Task.FromResult((OperationResult)command) : ExchangeAsync(command.Content, KeyenceNanoResponseParser.CheckPlcWriteResponse, token);

        /// <summary>PLC 기종을 읽습니다.</summary>
        public Task<OperationResult<KeyencePLCS>> ReadPlcTypeAsync(CancellationToken token = default(CancellationToken)) => ExchangeAsync(KeyenceNanoControlCommandBuilder.BuildReadPlcType(), KeyenceNanoControlResponseParser.ParseReadPlcType, token);
        /// <summary>PLC 운전 모드를 읽습니다.</summary>
        public Task<OperationResult<int>> ReadPlcModeAsync(CancellationToken token = default(CancellationToken)) => ExchangeAsync(KeyenceNanoControlCommandBuilder.BuildReadPlcMode(), KeyenceNanoControlResponseParser.ParseReadPlcMode, token);
        /// <summary>PLC 시각을 설정합니다.</summary>
        public Task<OperationResult> SetPlcDateTimeAsync(DateTime value, CancellationToken token = default(CancellationToken)) => ExchangeAsync(KeyenceNanoControlCommandBuilder.BuildSetPlcDateTime(value), r => KeyenceNanoControlResponseParser.ParseSetPlcDateTime(r, value), token);
        /// <summary>주소에 등록된 주석을 읽습니다.</summary>
        public Task<OperationResult<string>> ReadAddressAnnotationAsync(string address, CancellationToken token = default(CancellationToken)) => ExchangeAsync(KeyenceNanoControlCommandBuilder.BuildReadAddressAnnotation(address), r => KeyenceNanoControlResponseParser.ParseReadAddressAnnotation(r, address), token);
        /// <summary>확장 유닛의 메모리를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadExpansionMemoryAsync(byte unit, ushort address, ushort length, CancellationToken token = default(CancellationToken)) => ExchangeAsync(KeyenceNanoControlCommandBuilder.BuildReadExpansionMemory(unit, address, length), r => KeyenceNanoControlResponseParser.ParseReadExpansionMemory(r, unit, address, length), token);
        /// <summary>확장 유닛의 메모리에 기록합니다.</summary>
        public Task<OperationResult> WriteExpansionMemoryAsync(byte unit, ushort address, byte[] value, CancellationToken token = default(CancellationToken)) => WriteAsync(KeyenceNanoCommandBuilder.BuildWriteExpansionMemoryCommand(unit, address, value), token);

        /// <summary>CQ 명령으로 장치 세션을 종료합니다. 이후 호출자가 포트를 닫으세요.</summary>
        public Task<OperationResult> DisconnectAsync(CancellationToken token = default(CancellationToken)) => session.ExecuteAsync(async (tx, state) =>
        {
            if (!state.Connected)
                return OperationResult.CreateSuccessResult();
            byte[] response = await tx.ExchangeAsync(KeyenceNanoCommandBuilder.GetDisConnectCmd(station, useStation), boundary).ConfigureAwait(false);
            if (response.Length <= 2 || response[0] != 'C' || response[1] != 'F')
                return new OperationResult("연결 해제 응답이 CF가 아닙니다.");
            state.Connected = false;
            return OperationResult.CreateSuccessResult();
        }, timeout, token);

        private Task<T> ExchangeAsync<T>(byte[] command, Func<byte[], T> parse, CancellationToken token) =>
            ExecuteAsync(async tx => parse(await tx.ExchangeAsync(command, boundary).ConfigureAwait(false)), token);
        private Task<T> ExecuteAsync<T>(Func<ISerialTransaction, Task<T>> command, CancellationToken token) => session.ExecuteAsync(async (tx, state) =>
        {
            if (!state.Connected)
            {
                byte[] response = await tx.ExchangeAsync(KeyenceNanoCommandBuilder.GetConnectCmd(station, useStation), boundary).ConfigureAwait(false);
                if (response.Length <= 2 || response[0] != 'C' || response[1] != 'C')
                    throw new InvalidDataException("연결 수립 응답이 CC가 아닙니다.");
                state.Connected = true;
            }
            return await command(tx).ConfigureAwait(false);
        }, timeout, token);
        private sealed class Session { internal bool Connected; }
    }
}
