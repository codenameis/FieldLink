using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Xinje;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Xinje;

public class XinjeProtocolTests
{
    [Fact]
    public void BuildExtendedReadPdu_D100_1Word_MatchesTestVector()
    {
        var parsedAddress = XinjeAddress.Parse("D100", XinjeSeries.XdXl, isBitAccess: false);
        byte[] pdu = XinjeRequestBuilder.BuildExtendedReadPdu(parsedAddress, count: 1);

        // Expected PDU without station: 20 80 00 00 64 00 01
        byte[] expected = [0x20, 0x80, 0x00, 0x00, 0x64, 0x00, 0x01];
        Assert.Equal(expected, pdu);
    }

    [Fact]
    public void BuildExtendedReadPdu_M10_8Bits_MatchesTestVector()
    {
        var parsedAddress = XinjeAddress.Parse("M10", XinjeSeries.XdXl, isBitAccess: true);
        byte[] pdu = XinjeRequestBuilder.BuildExtendedReadPdu(parsedAddress, count: 8);

        // Expected PDU without station: 1E 03 00 00 0A 00 08
        byte[] expected = [0x1E, 0x03, 0x00, 0x00, 0x0A, 0x00, 0x08];
        Assert.Equal(expected, pdu);
    }

    [Fact]
    public void BuildExtendedWriteWordPdu_D100_1Word_MatchesTestVector()
    {
        var parsedAddress = XinjeAddress.Parse("D100", XinjeSeries.XdXl, isBitAccess: false);
        byte[] data = [0x34, 0x12];
        byte[] pdu = XinjeRequestBuilder.BuildExtendedWriteWordPdu(parsedAddress, count: 1, data);

        // Expected: 21 80 00 00 64 00 01 02 34 12
        byte[] expected = [0x21, 0x80, 0x00, 0x00, 0x64, 0x00, 0x01, 0x02, 0x34, 0x12];
        Assert.Equal(expected, pdu);
    }

    [Fact]
    public void BuildExtendedWriteBitPdu_M10_3Bits_MatchesTestVector()
    {
        var parsedAddress = XinjeAddress.Parse("M10", XinjeSeries.XdXl, isBitAccess: true);
        bool[] bits = [true, false, true]; // 1, 0, 1 -> 0x05
        byte[] pdu = XinjeRequestBuilder.BuildExtendedWriteBitPdu(parsedAddress, count: 3, bits);

        // Expected: 1F 03 00 00 0A 00 03 01 05
        byte[] expected = [0x1F, 0x03, 0x00, 0x00, 0x0A, 0x00, 0x03, 0x01, 0x05];
        Assert.Equal(expected, pdu);
    }

    [Fact]
    public async Task Protocol_ReadExtendedWord_Rtu_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var protocol = new XinjeProtocol(transport, new XinjeOptions { Station = 1 }, XinjeTransportEncoding.Rtu);

        // Response: Station 1, FC 0x20, ByteCount 2, Data 0x12 0x34 + CRC16
        byte[] response = FieldLink.Plc.Protocols.Modbus.ModbusRequestBuilder.BuildRtuAdu(1, [0x20, 0x02, 0x12, 0x34]);
        transport.EnqueueResponse(response);

        var requestFrame = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(requestFrame, default);

        Assert.Equal(new byte[] { 0x12, 0x34 }, result);
        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x01, sent[0]);
        Assert.Equal(0x20, sent[1]);
    }

    [Fact]
    public async Task Protocol_ReadStandardModbus_Xc_Roundtrip()
    {
        var transport = new ScriptedTransport();
        var options = new XinjeOptions { Series = XinjeSeries.XC, Station = 1 };
        var protocol = new XinjeProtocol(transport, options, XinjeTransportEncoding.Rtu);

        // Standard Modbus Holding Register response: 01 03 02 12 34 B5 33
        byte[] response = [0x01, 0x03, 0x02, 0x12, 0x34, 0xB5, 0x33];
        transport.EnqueueResponse(response);

        var requestFrame = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(requestFrame, default);

        Assert.Equal(new byte[] { 0x12, 0x34 }, result);
        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        Assert.Equal(0x01, sent[0]);
        Assert.Equal(0x03, sent[1]); // Standard FC 0x03
    }
}
