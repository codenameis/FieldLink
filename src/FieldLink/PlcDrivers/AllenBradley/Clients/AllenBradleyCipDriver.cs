using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Tcp;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers.AllenBradley.Clients
{
    internal sealed partial class AllenBradleyCipDriver : IPlcDriver
    {
        internal static readonly IFrameBoundary Frame = new HeaderLengthFrame(24, h =>
            24 + h.Array[h.Offset + 2] + h.Array[h.Offset + 3] * 256);
        private readonly TcpProtocolSession<Session> session;
        private readonly byte[] route;
        private readonly int writeLimit;

        internal AllenBradleyCipDriver(ITcpClient transport, AllenBradleyTcpClientOptions options)
        {
            if (options.MaxWriteBytesPerRequest < 8 || options.MaxWriteBytesPerRequest > 400 ||
                options.RoutePath != null && (options.RoutePath.Length > 64 || options.RoutePath.Length % 2 != 0))
                throw new ArgumentOutOfRangeException(nameof(options));
            route = options.RoutePath == null ? new byte[] { 1, options.Slot } : (byte[])options.RoutePath.Clone();
            writeLimit = options.MaxWriteBytesPerRequest;
            session = new TcpProtocolSession<Session>(transport, RegisterAsync);
        }
        public int StringAlignment => 1;
        public bool HasStringLength => true;
        public async Task InitializeAsync(TimeSpan timeout, CancellationToken token) =>
            await session.ExecuteAsync((tx, state) => Task.FromResult(true), timeout, token).ConfigureAwait(false);
        public async Task ShutdownAsync(TimeSpan timeout, CancellationToken token) =>
            await session.ExecuteAsync(async (tx, state) =>
            {
                await tx.SendAsync(AllenBradleyCommandBuilder.UnRegisterSessionHandle(state.Handle)).ConfigureAwait(false);
                tx.CloseAfterCompletion();
                return true;
            }, timeout, token).ConfigureAwait(false);

        private static async Task<Session> RegisterAsync(ITcpTransaction tx)
        {
            byte[] command = AllenBradleyCommandBuilder.RegisterSessionHandle(BitConverter.GetBytes(1L));
            byte[] response = await tx.ExchangeAsync(command, Frame, reply =>
            {
                ResponseDisposition envelope = Envelope(command, reply, false);
                if (envelope != ResponseDisposition.Accept || BitConverter.ToUInt32(reply, 8) != 0)
                    return envelope;
                return reply.Length == 28 && reply[24] == 1 && reply[25] == 0 && reply[26] == 0 && reply[27] == 0 &&
                    BitConverter.ToUInt32(reply, 4) != 0 ? ResponseDisposition.Accept : ResponseDisposition.Reject;
            }).ConfigureAwait(false);
            return new Session { Handle = PlcTransferRequest.Require(CipSessionResponseParser.ParseRegisterSession(response)), Sequence = 1 };
        }

        public Task<OperationResult<byte[]>> TransferAsync(PlcTransferRequest request, TimeSpan timeout, CancellationToken token)
        {
            ValidateTag(request.Address);
            if (request.Count > ushort.MaxValue)
                throw new PlcProtocolException(-1, "CIP 태그 요청의 원소 수는 최대 65535개입니다.");
            if (request.IsBit && (request.Count != 1 || request.Address.EndsWith("]", StringComparison.Ordinal)))
                throw new PlcProtocolException(-1, "Logix BOOL 스칼라 태그를 지정하세요. 압축 BOOL 배열은 저수준 비트 API를 사용합니다.");
            if (request.Type == PlcValueType.String)
            {
                ValidateTag(request.Address + ".LEN");
                ValidateTag(request.Address + ".DATA[0]");
            }
            byte[] payload = request.Encode();
            return session.ExecuteAsync((tx, state) => request.Type == PlcValueType.String
                ? TransferStringAsync(tx, state, request, payload)
                : payload == null
                    ? ReadTagAsync(tx, state, request.Address, TypeCode(request.Type), request.Count, request.ByteCount, request)
                    : WriteTagAsync(tx, state, request.Address, TypeCode(request.Type), request.Count, payload, request), timeout, token);
        }

        private static void ValidateTag(string address)
        {
            if (address.Length > 256 || !Regex.IsMatch(address,
                @"\A(?:Program:[A-Za-z_][A-Za-z_0-9]*\.)?[A-Za-z_][A-Za-z_0-9]*(?:\[[0-9]+(?:,[0-9]+){0,2}\])?(?:\.[A-Za-z_][A-Za-z_0-9]*(?:\[[0-9]+(?:,[0-9]+){0,2}\])?)*\z"))
                throw new PlcProtocolException(-1, "유효한 Logix 태그명·멤버·배열 인덱스를 지정하세요.");
            foreach (string segment in address.Split('.'))
            {
                string symbol = segment.Split('[')[0];
                if (symbol.StartsWith("Program:", StringComparison.Ordinal)) symbol = symbol.Substring(8);
                if (symbol.Length > 40)
                    throw new PlcProtocolException(-1, "Logix 태그/프로그램 이름은 최대 40자입니다.");
            }
            foreach (Match match in Regex.Matches(address, @"\[([0-9,]+)\]"))
                foreach (string index in match.Groups[1].Value.Split(','))
                    if (!int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                        throw new PlcProtocolException(-1, "CIP 배열 인덱스가 범위를 벗어납니다.");
            if (AllenBradleyCommandBuilder.BuildRequestPathCommand(address).Length > 350)
                throw new PlcProtocolException(-1, "CIP 태그 경로가 너무 깁니다.");
        }

        private static ushort TypeCode(PlcValueType type)
        {
            switch (type)
            {
                case PlcValueType.Boolean: return AllenBradleyDefinitions.CIP_Type_Bool;
                case PlcValueType.Bytes: return AllenBradleyDefinitions.CIP_Type_Byte;
                case PlcValueType.Int16: return AllenBradleyDefinitions.CIP_Type_Word;
                case PlcValueType.UInt16: return AllenBradleyDefinitions.CIP_Type_UInt;
                case PlcValueType.Int32: return AllenBradleyDefinitions.CIP_Type_DWord;
                case PlcValueType.UInt32: return AllenBradleyDefinitions.CIP_Type_UDint;
                case PlcValueType.Int64: return AllenBradleyDefinitions.CIP_Type_LInt;
                case PlcValueType.UInt64: return AllenBradleyDefinitions.CIP_Type_ULint;
                case PlcValueType.Float: return AllenBradleyDefinitions.CIP_Type_Real;
                case PlcValueType.Double: return AllenBradleyDefinitions.CIP_Type_Double;
                default: throw new InvalidOperationException("지원하지 않는 CIP 자료형입니다.");
            }
        }

        private sealed class Session
        {
            internal uint Handle;
            internal long Sequence;
        }
    }
}
