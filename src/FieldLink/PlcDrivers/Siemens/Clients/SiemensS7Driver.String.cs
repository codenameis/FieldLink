using System;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Tcp;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers.Siemens.Clients
{
    internal sealed partial class SiemensS7Driver
    {
        private async Task<OperationResult<byte[]>> TransferStringAsync(ITcpTransaction tx, Session state,
            S7DeviceAddress address, PlcTransferRequest request, byte[] payload)
        {
            bool smart = model == SiemensPLCS.S200Smart;
            int headerSize = smart ? 1 : 2;
            var header = await ExchangeAsync(tx, state, Offset(address, 0, headerSize), null, false).ConfigureAwait(false);
            if (!header.IsSuccess)
                return request.Failed(header);
            var length = S7StringCodec.GetReadLength(model, header.Content, false);
            if (!length.IsSuccess)
                return request.Failed(length);
            int actual = length.Content - headerSize;
            if (!request.IsWrite)
            {
                if (actual > request.Count)
                    return request.Failed(new OperationResult(-1, "STRING 현재 길이가 요청한 읽기 용량보다 큽니다."));
                var output = new byte[actual];
                int offset = 0;
                while (offset < actual)
                {
                    int block = Math.Min(state.PayloadLimit, actual - offset);
                    var data = await ExchangeAsync(tx, state, Offset(address, (headerSize + offset) * 8, block), null, false).ConfigureAwait(false);
                    if (!data.IsSuccess)
                        return request.Failed(data);
                    Array.Copy(data.Content, 0, output, offset, block);
                    request.Pending = block;
                    request.Confirm();
                    offset += block;
                }
                return OperationResult.CreateSuccessResult(output);
            }
            int maximum = smart ? 254 : header.Content[0];
            if (request.Count > maximum)
                return request.Failed(new OperationResult(-1, "쓰기 용량이 PLC에 선언된 STRING 최대 길이를 초과합니다."));
            var bytes = new byte[headerSize + payload.Length];
            if (!smart)
                bytes[0] = (byte)maximum;
            bytes[headerSize - 1] = (byte)request.TextLength;
            Array.Copy(payload, 0, bytes, headerSize, payload.Length);
            // 문자열 헤더와 데이터도 동일한 독점 구간·제한 시간을 사용한다. 분할 쓰기는 원자적이지 않다.
            while (request.Confirmed < bytes.Length)
            {
                int block = Math.Min(state.PayloadLimit, bytes.Length - request.Confirmed);
                var part = new byte[block];
                Array.Copy(bytes, request.Confirmed, part, 0, block);
                request.Pending = block;
                var reply = await ExchangeAsync(tx, state, Offset(address, request.Confirmed * 8, block), part, false).ConfigureAwait(false);
                if (!reply.IsSuccess)
                {
                    request.Pending = 0;
                    return request.Failed(reply);
                }
                request.Confirm();
            }
            return OperationResult.CreateSuccessResult(new byte[0]);
        }
    }
}
