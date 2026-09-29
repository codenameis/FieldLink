using FieldLink.PlcDrivers.Modbus;
using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Tcp;
using FieldLink.PlcDrivers.Common;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Modbus.Clients
{
    internal static class ModbusTcpExchange
    {
        internal static readonly IFrameBoundary Boundary = new HeaderLengthFrame(6, data =>
        {
            int length = data.Array[data.Offset + 4] * 256 + data.Array[data.Offset + 5];
            if (data.Array[data.Offset + 2] != 0 || data.Array[data.Offset + 3] != 0 || length < 2 || length > 254)
                throw new InvalidDataException("Modbus TCP MBAP 프로토콜 ID 또는 길이가 올바르지 않습니다.");
            return 6 + length;
        });
        internal static async Task<OperationResult<byte[]>> ExecuteAsync(ITcpTransaction tx, byte[] core, ushort id)
        {
            byte[] response = await tx.ExchangeAsync(ModbusFrameRules.PackCommandToTcp(core, id), Boundary,
                bytes => bytes.Length < 8 || bytes[2] != 0 || bytes[3] != 0 ? ResponseDisposition.Reject :
                    bytes[0] * 256 + bytes[1] == id ? ResponseDisposition.Accept : ResponseDisposition.Ignore).ConfigureAwait(false);
            return ModbusResponseParser.Parse(core, response.RemoveBegin(6));
        }
    }
}
