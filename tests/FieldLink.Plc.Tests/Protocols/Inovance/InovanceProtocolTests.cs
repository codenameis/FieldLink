using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Inovance;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Inovance;

public class InovanceProtocolTests
{
    [Fact]
    public async Task ReadAsync_H3u_D100_Tcp_BuildsMbapAndDecodesRegisters()
    {
        var transport = new ScriptedTransport();
        var protocol = new InovanceProtocol(transport, new InovanceOptions { Series = InovanceSeries.H3u, Station = 1 }, InovanceTransportEncoding.Tcp);

        // Enqueue TCP response for FC03, PDU 100, 1 word: value 0x1234
        transport.EnqueueResponse(frame =>
        {
            ushort transactionId = (ushort)((frame[0] << 8) | frame[1]);
            byte[] pdu = [0x03, 0x02, 0x12, 0x34];
            return ModbusRequestBuilder.BuildTcpAdu(transactionId, 1, pdu);
        });

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x12, result[0]);
        Assert.Equal(0x34, result[1]);

        Assert.Single(transport.SentFrames);
        byte[] pduSent = ModbusResponseParser.UnwrapTcpAdu(transport.SentFrames[0], 1, 1);
        Assert.Equal(0x03, pduSent[0]); // FC 03
        Assert.Equal(0x00, pduSent[1]); // PDU Hi (100 = 0x0064)
        Assert.Equal(0x64, pduSent[2]); // PDU Lo
    }

    [Fact]
    public async Task ReadAsync_Am_IX10_UsesDiscreteInputFC02()
    {
        var transport = new ScriptedTransport();
        var protocol = new InovanceProtocol(transport, new InovanceOptions { Series = InovanceSeries.AM, Station = 1 }, InovanceTransportEncoding.Tcp);

        transport.EnqueueResponse(frame =>
        {
            ushort transactionId = (ushort)((frame[0] << 8) | frame[1]);
            byte[] pdu = [0x02, 0x01, 0x01]; // FC 02, 1 byte, bit 0 is ON
            return ModbusRequestBuilder.BuildTcpAdu(transactionId, 1, pdu);
        });

        var request = new PlcReadRequest("IX10", PlcDataType.Boolean, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(result);
        Assert.Equal(1, result[0]);

        Assert.Single(transport.SentFrames);
        byte[] pduSent = ModbusResponseParser.UnwrapTcpAdu(transport.SentFrames[0], 1, 1);
        Assert.Equal(0x02, pduSent[0]); // FC 02 (Discrete Input)
        Assert.Equal(0x00, pduSent[1]);
        Assert.Equal(10, pduSent[2]); // PDU 10
    }
}
