using System;
using System.IO;
using System.Threading.Tasks;
using FieldLink.Communication.Tcp;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers.AllenBradley.Clients
{
    internal sealed partial class AllenBradleyCipDriver
    {
        private async Task<OperationResult<byte[]>> ReadTagAsync(ITcpTransaction tx, Session state, string address,
            ushort type, int count, int byteCount, PlcTransferRequest request)
        {
            var output = new byte[byteCount];
            int offset = 0;
            while (offset < byteCount)
            {
                byte[] cip = offset == 0 ? AllenBradleyCommandBuilder.PackRequsetRead(address, count)
                    : AllenBradleyCommandBuilder.PackRequestReadSegment(address, offset, count);
                var reply = await ExchangeAsync(tx, state, cip, true, type).ConfigureAwait(false);
                if (!reply.Result.IsSuccess)
                    return request.Failed(reply.Result);
                byte[] bytes = reply.Result.Content;
                if (bytes.Length == 0 || bytes.Length > byteCount - offset ||
                    (reply.More ? bytes.Length == byteCount - offset : bytes.Length != byteCount - offset))
                    throw new InvalidDataException("CIP 응답 길이와 분할 상태가 요청 범위와 일치하지 않습니다.");
                Array.Copy(bytes, 0, output, offset, bytes.Length);
                offset += bytes.Length;
                request.Pending = bytes.Length;
                request.Confirm();
            }
            return OperationResult.CreateSuccessResult(output);
        }

        private async Task<OperationResult<byte[]>> WriteTagAsync(ITcpTransaction tx, Session state, string address,
            ushort type, int count, byte[] payload, PlcTransferRequest request)
        {
            int pathLength = AllenBradleyCommandBuilder.BuildRequestPathCommand(address).Length;
            int limit = Math.Min(writeLimit, 480 - pathLength - route.Length - 24);
            int width = payload.Length / count;
            limit -= limit % width;
            bool fragmented = payload.Length > limit;
            int offset = 0;
            while (offset < payload.Length)
            {
                int block = Math.Min(limit, payload.Length - offset);
                var part = new byte[block];
                Array.Copy(payload, offset, part, 0, block);
                byte[] cip = fragmented ? AllenBradleyCommandBuilder.PackRequestWriteSegment(address, type, part, offset, count)
                    : AllenBradleyCommandBuilder.PackRequestWrite(address, type, part, count);
                request.Pending = block;
                var reply = await ExchangeAsync(tx, state, cip, false, type).ConfigureAwait(false);
                if (!reply.Result.IsSuccess)
                {
                    request.Pending = 0;
                    return request.Failed(reply.Result);
                }
                offset += block;
                request.Confirm();
            }
            return OperationResult.CreateSuccessResult(new byte[0]);
        }

        private async Task<OperationResult<byte[]>> TransferStringAsync(ITcpTransaction tx, Session state,
            PlcTransferRequest request, byte[] payload)
        {
            string lengthTag = request.Address + ".LEN", dataTag = request.Address + ".DATA[0]";
            if (request.IsWrite)
            {
                // LEN은 DATA의 모든 조각이 확인된 뒤에 갱신한다. PLC 스캔과의 원자성은 보장하지 않는다.
                var data = await WriteTagAsync(tx, state, dataTag, 0xC2, payload.Length, payload, request).ConfigureAwait(false);
                if (!data.IsSuccess)
                    return data;
                return await WriteTagAsync(tx, state, lengthTag, 0xC4, 1, BitConverter.GetBytes(request.TextLength), request).ConfigureAwait(false);
            }
            var length = await ReadTagAsync(tx, state, lengthTag, 0xC4, 1, 4, request).ConfigureAwait(false);
            if (!length.IsSuccess)
                return length;
            int count = BitConverter.ToInt32(length.Content, 0);
            if (count < 0 || count > request.Count)
                return request.Failed(new OperationResult(-1, "Logix STRING LEN이 지정한 읽기 용량을 벗어납니다."));
            return count == 0 ? OperationResult.CreateSuccessResult(new byte[0])
                : await ReadTagAsync(tx, state, dataTag, 0xC2, count, count, request).ConfigureAwait(false);
        }
    }
}
