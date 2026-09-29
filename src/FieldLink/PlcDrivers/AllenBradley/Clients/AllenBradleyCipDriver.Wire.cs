using System;
using System.IO;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Tcp;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers.AllenBradley.Clients
{
    internal sealed partial class AllenBradleyCipDriver
    {
        private static ResponseDisposition Envelope(byte[] command, byte[] reply, bool checkSession)
        {
            if (reply.Length < 24 || reply.Length != 24 + reply[2] + reply[3] * 256 ||
                reply[0] != command[0] || reply[1] != command[1] || BitConverter.ToUInt32(reply, 20) != 0)
                return ResponseDisposition.Reject;
            for (int i = 12; i < 20; i++)
                if (reply[i] != command[i]) return ResponseDisposition.Ignore;
            if (checkSession && BitConverter.ToUInt32(reply, 8) == 0 && BitConverter.ToUInt32(reply, 4) != BitConverter.ToUInt32(command, 4))
                return ResponseDisposition.Reject;
            return ResponseDisposition.Accept;
        }

        private async Task<Reply> ExchangeAsync(ITcpTransaction tx, Session state, byte[] cip, bool read, ushort type)
        {
            byte[] service = route.Length == 0 ? AllenBradleyCommandBuilder.PackCommandSingleService(cip)
                : AllenBradleyCommandBuilder.PackCommandService(route, cip);
            byte[] body = AllenBradleyCommandBuilder.PackCommandSpecificData(new byte[4], service);
            byte[] command = AllenBradleyCommandBuilder.PackRequestHeader(0x6F, state.Handle, body, BitConverter.GetBytes(unchecked(++state.Sequence)));
            Reply result = null;
            byte[] response = await tx.ExchangeAsync(command, Frame, reply =>
            {
                ResponseDisposition envelope = Envelope(command, reply, true);
                if (envelope != ResponseDisposition.Accept || BitConverter.ToUInt32(reply, 8) != 0)
                    return envelope;
                // Single unconnected CIP reply: null address item and one 0xB2 data item.
                if (reply.Length < 44 || BitConverter.ToUInt32(reply, 24) != 0 ||
                    BitConverter.ToUInt16(reply, 30) != 2 || BitConverter.ToUInt32(reply, 32) != 0 ||
                    BitConverter.ToUInt16(reply, 36) != 0xB2 || BitConverter.ToUInt16(reply, 38) != reply.Length - 40 || reply[41] != 0)
                    return ResponseDisposition.Reject;
                int dataStart = 44 + reply[43] * 2;
                if (dataStart > reply.Length)
                    return ResponseDisposition.Reject;
                byte status = reply[42];
                bool routeError = route.Length != 0 && reply[40] == 0xD2 && status != 0;
                if (reply[40] != (cip[0] | 0x80) && !routeError)
                    return ResponseDisposition.Reject;
                bool partialRead = read && status == 6 && reply[40] == (cip[0] | 0x80);
                if (status != 0 && !partialRead)
                {
                    string extra = reply[43] == 0 ? "" : "; Extended=" + BitConverter.ToString(reply, 44, reply[43] * 2);
                    result = new Reply { Result = new OperationResult<byte[]>(status, "CIP status=0x" + status.ToString("X2") + extra) };
                    return ResponseDisposition.Accept;
                }
                if (!read)
                {
                    if (dataStart != reply.Length) return ResponseDisposition.Reject;
                    result = new Reply { Result = OperationResult.CreateSuccessResult(new byte[0]) };
                    return ResponseDisposition.Accept;
                }
                if (dataStart + 2 > reply.Length)
                    return ResponseDisposition.Reject;
                ushort actualType = BitConverter.ToUInt16(reply, dataStart);
                // Logix BOOL의 상위 바이트는 숨겨진 SINT 안의 비트 위치(0~7)를 포함할 수 있다.
                if (actualType != type && !(type == 0xC1 && (actualType & 0xF8FF) == 0xC1))
                    return ResponseDisposition.Reject;
                var data = new byte[reply.Length - dataStart - 2];
                Array.Copy(reply, dataStart + 2, data, 0, data.Length);
                result = new Reply { Result = OperationResult.CreateSuccessResult(data), More = partialRead };
                return ResponseDisposition.Accept;
            }).ConfigureAwait(false);
            OperationResult encapsulation = AllenBradleyResponseParser.CheckResponse(response);
            if (!encapsulation.IsSuccess)
                throw new PlcProtocolException(encapsulation.ErrorCode, encapsulation.Message);
            return result;
        }

        private sealed class Reply
        {
            internal OperationResult<byte[]> Result;
            internal bool More;
        }
    }
}
