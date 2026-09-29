using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers.Modbus.Clients
{
    internal sealed class ModbusTcpDriver : IPlcDriver
    {
        internal static readonly IFrameBoundary Frame = ModbusTcpExchange.Boundary;
        private readonly ITcpClient transport;
        private readonly byte unit;
        private readonly bool zeroBased;
        private readonly int maxWords;
        private readonly int maxBits;
        private ushort sequence;

        internal ModbusTcpDriver(ITcpClient transport, ModbusTcpClientOptions options)
        {
            this.transport = transport;
            if (options.UnitId == 0 || options.MaxRegistersPerRequest < 1 || options.MaxRegistersPerRequest > 120 ||
                options.MaxBitsPerRequest < 1 || options.MaxBitsPerRequest > 1968)
                throw new ArgumentOutOfRangeException(nameof(options));
            unit = options.UnitId;
            zeroBased = options.AddressStartWithZero;
            maxWords = options.MaxRegistersPerRequest;
            maxBits = options.MaxBitsPerRequest;
        }
        public int StringAlignment => 2;
        public bool HasStringLength => false;
        public Task InitializeAsync(TimeSpan timeout, CancellationToken token) => Task.CompletedTask;
        public Task ShutdownAsync(TimeSpan timeout, CancellationToken token) => Task.CompletedTask;

        public Task<OperationResult<byte[]>> TransferAsync(PlcTransferRequest request, TimeSpan timeout, CancellationToken token)
        {
            ModbusAddress address;
            try { address = new ModbusAddress(request.Address, unit, request.IsBit ? (byte)1 : (byte)3); }
            catch (FormatException error) { throw new PlcProtocolException(-1, error.Message); }
            catch (OverflowException error) { throw new PlcProtocolException(-1, error.Message); }
            int start = address.AddressStart - (zeroBased ? 0 : 1);
            int count = request.IsBit ? request.Count : request.ByteCount / 2;
            if ((!request.IsBit && request.ByteCount % 2 != 0) || start < 0 || count > 65536 - start || address.Station == 0)
                throw new PlcProtocolException(-1, "Modbus 범위/Unit ID 또는 워드 바이트 길이가 올바르지 않습니다.");
            if (request.IsWrite && (address.WriteFunction > 0 && address.WriteFunction != (request.IsBit ? 15 : 16)))
                throw new PlcProtocolException(-1, "간편 연속 쓰기는 비트 FC15, 레지스터 FC16을 사용합니다.");
            if (request.IsBit ? address.Function != 1 && address.Function != 2 : address.Function != 3 && address.Function != 4)
                throw new PlcProtocolException(-1, "요청 자료형과 읽기 기능 코드가 일치하지 않습니다.");
            if (request.IsWrite && (address.Function == 2 || address.Function == 4))
                throw new PlcProtocolException(-1, "입력 접점/입력 레지스터는 쓰기 대상이 아닙니다.");
            request.Unit = request.IsBit ? "bit" : "word";
            byte[] payload = request.Encode();
            return transport.ExecuteTransactionAsync(async tx =>
            {
                var output = request.IsWrite ? new byte[0] : new byte[request.ByteCount];
                int limit = request.IsBit ? maxBits : maxWords;
                while (request.Confirmed < count)
                {
                    int block = Math.Min(limit, count - request.Confirmed);
                    string location = (start + request.Confirmed).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    byte[] core;
                    if (!request.IsWrite)
                        core = PlcTransferRequest.Require(ModbusCommandBuilder.BuildReadModbusCommand(location, (ushort)block,
                            (byte)address.Station, true, (byte)address.Function))[0];
                    else if (request.IsBit)
                    {
                        var bits = new bool[block];
                        for (int i = 0; i < block; i++)
                            bits[i] = payload[request.Confirmed + i] != 0;
                        core = PlcTransferRequest.Require(ModbusCommandBuilder.BuildWriteBoolModbusCommand(location, bits, (byte)address.Station, true, 15));
                    }
                    else
                    {
                        var bytes = new byte[block * 2];
                        Array.Copy(payload, request.Confirmed * 2, bytes, 0, bytes.Length);
                        core = PlcTransferRequest.Require(ModbusCommandBuilder.BuildWriteWordModbusCommand(location, bytes, (byte)address.Station, true, 16));
                    }
                    request.Pending = block;
                    ushort id = unchecked(++sequence);
                    OperationResult<byte[]> parsed = null;
                    await tx.ExchangeAsync(ModbusFrameRules.PackCommandToTcp(core, id), Frame, response =>
                    {
                        if (response.Length < 8 || response[2] != 0 || response[3] != 0 ||
                            response.Length != 6 + response[4] * 256 + response[5])
                            return ResponseDisposition.Reject;
                        if (response[0] * 256 + response[1] != id)
                            return ResponseDisposition.Ignore;
                        var content = new byte[response.Length - 6];
                        Array.Copy(response, 6, content, 0, content.Length);
                        parsed = ModbusResponseParser.Parse(core, content);
                        bool deviceError = content.Length == 3 && content[0] == core[0] && content[1] == (core[1] | 0x80) && content[2] != 0;
                        return parsed.IsSuccess || deviceError ? ResponseDisposition.Accept : ResponseDisposition.Reject;
                    }).ConfigureAwait(false);
                    if (!parsed.IsSuccess)
                    {
                        request.Pending = 0;
                        return request.Failed(parsed);
                    }
                    if (!request.IsWrite)
                    {
                        if (request.IsBit)
                            for (int i = 0; i < block; i++)
                                output[request.Confirmed + i] = (byte)((parsed.Content[i / 8] >> (i % 8)) & 1);
                        else
                            Array.Copy(parsed.Content, 0, output, request.Confirmed * 2, block * 2);
                    }
                    request.Confirm();
                }
                return OperationResult.CreateSuccessResult(output);
            }, timeout, token);
        }
    }
}
