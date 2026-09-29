using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers.Omron.Clients
{
    internal sealed class FinsTcpDriver : IPlcDriver
    {
        private readonly FinsTcpSession session;
        private readonly OmronPlcType model;
        private readonly int maxWords;
        private readonly int maxBits;
        internal FinsTcpDriver(ITcpClient transport, FinsTcpClientOptions options)
        {
            if (!Enum.IsDefined(typeof(OmronPlcType), options.PlcType) ||
                options.MaxWordsPerRequest < 1 || options.MaxWordsPerRequest > 500 ||
                options.MaxBitsPerRequest < 1 || options.MaxBitsPerRequest > 1990)
                throw new ArgumentOutOfRangeException(nameof(options));
            session = new FinsTcpSession(transport, options.Timeout);
            model = options.PlcType;
            maxWords = options.MaxWordsPerRequest;
            maxBits = options.MaxBitsPerRequest;
        }
        public int StringAlignment => 2;
        public bool HasStringLength => false;
        public async Task InitializeAsync(TimeSpan timeout, CancellationToken token) =>
            PlcTransferRequest.Require(await session.ExecuteAsync((_, __) =>
                Task.FromResult(OperationResult.CreateSuccessResult(true)), token, timeout).ConfigureAwait(false));
        public Task ShutdownAsync(TimeSpan timeout, CancellationToken token) => Task.CompletedTask;

        public async Task<OperationResult<byte[]>> TransferAsync(PlcTransferRequest request, TimeSpan timeout, CancellationToken token)
        {
            var address = PlcTransferRequest.Require(OmronFinsAddress.ParseFrom(request.Address, 1, model));
            int count = request.IsBit ? request.Count : request.ByteCount / 2;
            int step = request.IsBit ? 1 : 16;
            if ((!request.IsBit && (request.ByteCount % 2 != 0 || address.AddressStart % 16 != 0)) ||
                address.AddressStart < 0 || (long)address.AddressStart + (long)count * step > 65536L * 16)
                throw new PlcProtocolException(-1, "FINS 메모리 범위 또는 워드 정렬이 올바르지 않습니다.");
            request.Unit = request.IsBit ? "bit" : "word";
            byte[] data = request.Encode();
            var transfer = await session.ExecuteAsync(async (tx, options) =>
            {
                var output = request.IsWrite ? new byte[0] : new byte[request.ByteCount];
                int warning = 0;
                while (request.Confirmed < count)
                {
                    int block = Math.Min(count - request.Confirmed, request.IsBit ? maxBits : maxWords);
                    var location = new OmronFinsAddress { AddressStart = address.AddressStart + request.Confirmed * step,
                        WordCode = address.WordCode, BitCode = address.BitCode };
                    byte[] core = OmronFinsNetCommandBuilder.BuildReadCommand(location, (ushort)block, request.IsBit);
                    int bytes = block * (request.IsBit ? 1 : 2);
                    if (request.IsWrite)
                    {
                        // 메모리 쓰기(01 02)는 읽기와 같은 영역/시작점/개수 뒤에 데이터를 붙인다.
                        Array.Resize(ref core, 8 + bytes);
                        core[1] = 2;
                        Array.Copy(data, request.Confirmed * (request.IsBit ? 1 : 2), core, 8, bytes);
                    }
                    request.Pending = block;
                    var reply = await FinsTcpSession.ExchangeCoreAsync(tx, options, core, request.IsWrite ? 0 : bytes,
                        request.IsBit && !request.IsWrite).ConfigureAwait(false);
                    if (!reply.IsSuccess)
                    {
                        request.Pending = 0;
                        return request.Failed(reply);
                    }
                    warning |= reply.ErrorCode;
                    if (!request.IsWrite)
                        Array.Copy(reply.Content, 0, output, request.Confirmed * (request.IsBit ? 1 : 2), bytes);
                    request.Confirm();
                }
                var result = OperationResult.CreateSuccessResult(output);
                result.ErrorCode = warning;
                return result;
            }, token, timeout).ConfigureAwait(false);
            return transfer.IsSuccess ? transfer : request.Failed(transfer);
        }
    }
}
