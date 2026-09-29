using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Tcp;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers.Siemens.Clients
{
    internal sealed partial class SiemensS7Driver : IPlcDriver
    {
        internal static readonly IFrameBoundary Frame = new HeaderLengthFrame(4, h =>
        {
            if (h.Array[h.Offset] != 3 || h.Array[h.Offset + 1] != 0)
                throw new InvalidDataException("TPKT 헤더가 올바르지 않습니다.");
            return h.Array[h.Offset + 2] * 256 + h.Array[h.Offset + 3];
        });
        private readonly TcpProtocolSession<Session> session;
        private readonly SiemensPLCS model;
        private readonly byte[] connection;
        private readonly byte[] setup;

        internal SiemensS7Driver(ITcpClient transport, SiemensS7ClientOptions options)
        {
            if (!Enum.IsDefined(typeof(SiemensPLCS), options.Model))
                throw new ArgumentOutOfRangeException(nameof(options));
            model = options.Model;
            connection = S7SessionCodec.BuildConnection(model, options.RackSlot, options.LocalTsap, options.RemoteTsap);
            setup = S7SessionCodec.BuildSetup(model);
            session = new TcpProtocolSession<Session>(transport, InitializeSessionAsync);
        }
        public int StringAlignment => 1;
        public bool HasStringLength => true;
        public async Task InitializeAsync(TimeSpan timeout, CancellationToken token) =>
            await session.ExecuteAsync((tx, state) => Task.FromResult(true), timeout, token).ConfigureAwait(false);
        public Task ShutdownAsync(TimeSpan timeout, CancellationToken token) => Task.CompletedTask;

        private async Task<Session> InitializeSessionAsync(ITcpTransaction tx)
        {
            await tx.ExchangeAsync(connection, Frame, response => response.Length >= 11 &&
                response[4] + 5 == response.Length && response[5] == 0xD0 &&
                response[6] == connection[8] && response[7] == connection[9] && response[10] == 0
                ? ResponseDisposition.Accept : ResponseDisposition.Reject).ConfigureAwait(false);
            byte[] negotiated = await tx.ExchangeAsync(setup, Frame, response =>
                response.Length >= 19 && response[11] == setup[11] && response[12] == setup[12]
                ? ResponseDisposition.Accept : ResponseDisposition.Reject).ConfigureAwait(false);
            OperationResult envelope = S7ResponseValidation.ValidateEnvelope(negotiated);
            if (!envelope.IsSuccess)
            {
                if (envelope.ErrorCode == 10000)
                    throw new InvalidDataException(envelope.Message);
                throw new PlcProtocolException(envelope.ErrorCode, envelope.Message);
            }
            return new Session { PayloadLimit = Math.Min(8191, S7SessionCodec.ParsePayloadLimit(negotiated)) };
        }

        public Task<OperationResult<byte[]>> TransferAsync(PlcTransferRequest request, TimeSpan timeout, CancellationToken token)
        {
            S7DeviceAddress address = PlcTransferRequest.Require(S7DeviceAddress.ParseFrom(request.Address, 1));
            if (address.DataCode >= 0x1C && address.DataCode <= 0x1F)
                throw new PlcProtocolException(-1, "간편 API의 숫자 접근은 DB/M/I/Q/V 바이트 메모리를 사용하세요. C/T 전용 형식은 저수준 API를 사용합니다.");
            if (!request.IsBit && address.AddressStart % 8 != 0)
                throw new PlcProtocolException(-1, "숫자·문자열·원시 바이트에는 바이트 경계 주소가 필요합니다.");
            int byteCount = request.ByteCount;
            if (request.Type == PlcValueType.String)
            {
                if (request.Count > 254)
                    throw new PlcProtocolException(-1, "S7 STRING 용량은 최대 254바이트입니다.");
                byteCount += model == SiemensPLCS.S200Smart ? 1 : 2;
            }
            long last = (long)address.AddressStart + (request.IsBit ? request.Count - 1 : byteCount * 8L - 1);
            if (last > 0xFFFFFF)
                throw new PlcProtocolException(-1, "S7 주소 범위를 벗어납니다.");
            request.Unit = request.IsBit ? "bit" : "byte";
            byte[] payload = request.Encode();
            return session.ExecuteAsync((tx, state) => request.Type == PlcValueType.String
                ? TransferStringAsync(tx, state, address, request, payload)
                : TransferMemoryAsync(tx, state, address, request, payload), timeout, token);
        }

        private static S7DeviceAddress Offset(S7DeviceAddress address, int bits, int length) => new S7DeviceAddress
        { DataCode = address.DataCode, DbBlock = address.DbBlock, AddressStart = address.AddressStart + bits, Length = (ushort)length };

        private static async Task<OperationResult<byte[]>> ExchangeAsync(ITcpTransaction tx, Session state,
            S7DeviceAddress address, byte[] payload, bool bit)
        {
            ushort id = unchecked(++state.Sequence);
            bool write = payload != null;
            byte[] command = write
                ? PlcTransferRequest.Require(bit ? SiemensS7NetCommandBuilder.BuildWriteBitCommand(address.ToString(), payload[0] != 0, id)
                    : SiemensS7NetCommandBuilder.BuildWriteByteCommand(address, payload, id))
                : PlcTransferRequest.Require(bit ? SiemensS7NetCommandBuilder.BuildBitReadCommand(address.ToString(), id)
                    : SiemensS7NetCommandBuilder.BuildReadCommand(new[] { address }, id));
            OperationResult<byte[]> result = null;
            await tx.ExchangeAsync(command, Frame, response =>
            {
                if (response.Length < 19 || response.Length > state.PayloadLimit + 35)
                    return ResponseDisposition.Reject;
                if (response[11] != command[11] || response[12] != command[12])
                    return ResponseDisposition.Ignore;
                OperationResult envelope = S7ResponseValidation.ValidateEnvelope(response);
                if (!envelope.IsSuccess)
                {
                    result = envelope.ConvertFailed<byte[]>();
                    return envelope.ErrorCode == 10000 ? ResponseDisposition.Reject : ResponseDisposition.Accept;
                }
                if (response.Length < 21 || response[20] != 1)
                    return ResponseDisposition.Reject;
                if (write)
                {
                    OperationResult parsed = SiemensS7NetResponseParser.AnalysisWrite(response);
                    result = parsed.IsSuccess ? OperationResult.CreateSuccessResult(new byte[0]) : parsed.ConvertFailed<byte[]>();
                }
                else
                    result = bit ? SiemensS7ResponseParser.AnalysisReadBit(response)
                        : SiemensS7NetResponseParser.AnalysisReadByte(new[] { address }, response);
                if (result.IsSuccess && !write && (result.Content.Length != (bit ? 1 : address.Length) || bit && result.Content[0] > 1))
                    return ResponseDisposition.Reject;
                return result.IsSuccess || result.ErrorCode != 10000 ? ResponseDisposition.Accept : ResponseDisposition.Reject;
            }).ConfigureAwait(false);
            return result;
        }

        private static async Task<OperationResult<byte[]>> TransferMemoryAsync(ITcpTransaction tx, Session state,
            S7DeviceAddress address, PlcTransferRequest request, byte[] payload)
        {
            int count = request.ByteCount;
            var output = request.IsWrite ? new byte[0] : new byte[count];
            while (request.Confirmed < count)
            {
                int block = Math.Min(request.IsBit ? 1 : state.PayloadLimit, count - request.Confirmed);
                var location = Offset(address, request.Confirmed * (request.IsBit ? 1 : 8), block);
                byte[] data = null;
                if (request.IsWrite)
                {
                    data = new byte[block];
                    Array.Copy(payload, request.Confirmed, data, 0, block);
                }
                request.Pending = block;
                var reply = await ExchangeAsync(tx, state, location, data, request.IsBit).ConfigureAwait(false);
                if (!reply.IsSuccess)
                {
                    request.Pending = 0;
                    return request.Failed(reply);
                }
                if (!request.IsWrite)
                    Array.Copy(reply.Content, 0, output, request.Confirmed, block);
                request.Confirm();
            }
            return OperationResult.CreateSuccessResult(output);
        }

        private sealed class Session
        {
            internal int PayloadLimit;
            internal ushort Sequence;
        }
    }
}
