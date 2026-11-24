using System.Threading.Tasks;
using FieldLink.Plc.Abstractions;
using FieldLink.Plc.Protocols.Jtekt;
using FieldLink.Plc.Tests.Support;
using Xunit;

namespace FieldLink.Plc.Tests.Protocols.Jtekt;

public class ToyopucProtocolTests
{
    [Fact]
    public async Task ReadAsync_D100_1Word_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        // Response vector from §4: 80 00 03 00 1C 34 12
        byte[] response = [0x80, 0x00, 0x03, 0x00, 0x1C, 0x34, 0x12];
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x34, result[0]);
        Assert.Equal(0x12, result[1]);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        // Request vector from §4: 00 00 05 00 1C 00 11 01 00
        byte[] expectedVector = [0x00, 0x00, 0x05, 0x00, 0x1C, 0x00, 0x11, 0x01, 0x00];
        Assert.Equal(expectedVector, sent);
    }

    [Fact]
    public async Task WriteAsync_D100_2Words_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        byte[] response = [0x80, 0x00, 0x01, 0x00, 0x1D];
        transport.EnqueueResponse(response);

        byte[] data = [0x34, 0x12, 0xAB, 0xCD];
        var request = new PlcWriteRequest("D100", PlcDataType.Int16, data);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        // Request vector from §4: 00 00 07 00 1D 00 11 34 12 AB CD
        byte[] expectedVector = [0x00, 0x00, 0x07, 0x00, 0x1D, 0x00, 0x11, 0x34, 0x12, 0xAB, 0xCD];
        Assert.Equal(expectedVector, sent);
    }

    [Fact]
    public async Task ReadAsync_M10_Bit_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        byte[] response = [0x80, 0x00, 0x02, 0x00, 0x20, 0x01];
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("M10", PlcDataType.Boolean, 1);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Single(result);
        Assert.Equal(1, result[0]);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        // Request vector from §4: 00 00 03 00 20 10 18
        byte[] expectedVector = [0x00, 0x00, 0x03, 0x00, 0x20, 0x10, 0x18];
        Assert.Equal(expectedVector, sent);
    }

    [Fact]
    public async Task ReadAsync_MultipleBasicBits_SequentiallyReadsWith0x20()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        transport.EnqueueResponse([0x80, 0x00, 0x02, 0x00, 0x20, 0x01]);
        transport.EnqueueResponse([0x80, 0x00, 0x02, 0x00, 0x20, 0x00]);
        transport.EnqueueResponse([0x80, 0x00, 0x02, 0x00, 0x20, 0x01]);

        var request = new PlcReadRequest("M10", PlcDataType.Boolean, 3);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 1, 0, 1 }, result);
        Assert.Equal(3, transport.SentFrames.Count);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x03, 0x00, 0x20, 0x10, 0x18 }, transport.SentFrames[0]);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x03, 0x00, 0x20, 0x11, 0x18 }, transport.SentFrames[1]);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x03, 0x00, 0x20, 0x12, 0x18 }, transport.SentFrames[2]);
    }

    [Fact]
    public async Task WriteAsync_M10_Bit_MatchesTestVector()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        byte[] response = [0x80, 0x00, 0x01, 0x00, 0x21];
        transport.EnqueueResponse(response);

        byte[] bitData = [1];
        var request = new PlcWriteRequest("M10", PlcDataType.Boolean, bitData);
        await protocol.WriteAsync(request, default);

        Assert.Single(transport.SentFrames);
        byte[] sent = transport.SentFrames[0];
        // Request vector from §4: 00 00 04 00 21 10 18 01
        byte[] expectedVector = [0x00, 0x00, 0x04, 0x00, 0x21, 0x10, 0x18, 0x01];
        Assert.Equal(expectedVector, sent);
    }

    [Fact]
    public async Task WriteAsync_MultipleBasicBits_SequentiallyWritesWith0x21AndPreservesCompletedCountOnError()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        transport.EnqueueResponse([0x80, 0x00, 0x01, 0x00, 0x21]);
        transport.EnqueueResponse([0x80, 0x31, 0x01, 0x00, 0x31]);

        byte[] bitData = [1, 0, 1];
        var request = new PlcWriteRequest("M10", PlcDataType.Boolean, bitData);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, default));

        Assert.Equal(0x31, exception.DeviceErrorCode);
        Assert.Equal(1, exception.CompletedCount);
        Assert.Equal(2, transport.SentFrames.Count);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x04, 0x00, 0x21, 0x10, 0x18, 0x01 }, transport.SentFrames[0]);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x04, 0x00, 0x21, 0x11, 0x18, 0x00 }, transport.SentFrames[1]);
    }

    [Fact]
    public async Task ReadAsync_ExtendedBit_ReadsWordsWith0x94AndExtractsBits()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        // EM1E -> word index 1 (wire 0x0601), bit offset 14 (0xE). Reading 4 bits spans 2 words (0x0601, 0x0602).
        // Word 0: bit 14=1, bit 15=0 -> 0x4000 (LE: 0x00, 0x40)
        // Word 1: bit 0=1, bit 1=1 -> 0x0003 (LE: 0x03, 0x00)
        byte[] response = [0x80, 0x00, 0x05, 0x00, 0x94, 0x00, 0x40, 0x03, 0x00];
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("EM1E", PlcDataType.Boolean, 4);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 1, 0, 1, 1 }, result);
        Assert.Single(transport.SentFrames);
        // CMD 0x94, PRG 0x00, wire 0x0601 (LE: 01 06), wordCount 2 (LE: 02 00)
        Assert.Equal(new byte[] { 0x00, 0x00, 0x06, 0x00, 0x94, 0x00, 0x01, 0x06, 0x02, 0x00 }, transport.SentFrames[0]);
    }

    [Fact]
    public async Task WriteAsync_ExtendedBit_PerformsReadModifyWriteWith0x94And0x95()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        // EM10 -> word index 1 (wire 0x0601), bit offset 0. Write 2 bits: [1, 0]
        // Existing word: 0x00F2 (LE: 0xF2, 0x00) -> bit 0=0, bit 1=1
        // After setting bit 0=1, bit 1=0 -> 0x00F1 (LE: 0xF1, 0x00)
        transport.EnqueueResponse([0x80, 0x00, 0x03, 0x00, 0x94, 0xF2, 0x00]);
        transport.EnqueueResponse([0x80, 0x00, 0x01, 0x00, 0x95]);

        var request = new PlcWriteRequest("EM10", PlcDataType.Boolean, [1, 0]);
        await protocol.WriteAsync(request, default);

        Assert.Equal(2, transport.SentFrames.Count);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x06, 0x00, 0x94, 0x00, 0x01, 0x06, 0x01, 0x00 }, transport.SentFrames[0]);
        Assert.Equal(new byte[] { 0x00, 0x00, 0x06, 0x00, 0x95, 0x00, 0x01, 0x06, 0xF1, 0x00 }, transport.SentFrames[1]);
    }

    [Fact]
    public async Task ReadAsync_Int32MultiChunk_ChunksAlignedByElementAndAdvancesWords()
    {
        var transport = new ScriptedTransport();
        // MaxReadWords = 5 -> wordsPerElement(Int32) = 2 -> maxElementsPerChunk = 2 (4 words per chunk)
        var protocol = new ToyopucProtocol(transport, new ToyopucOptions { MaxReadWords = 5 });

        // Chunk 1: 4 words (8 bytes)
        transport.EnqueueResponse([0x80, 0x00, 0x09, 0x00, 0x1C, 1, 0, 2, 0, 3, 0, 4, 0]);
        // Chunk 2: 2 words (4 bytes)
        transport.EnqueueResponse([0x80, 0x00, 0x05, 0x00, 0x1C, 5, 0, 6, 0]);

        var request = new PlcReadRequest("D100", PlcDataType.Int32, 3);
        byte[] result = await protocol.ReadAsync(request, default);

        Assert.Equal(new byte[] { 1, 0, 2, 0, 3, 0, 4, 0, 5, 0, 6, 0 }, result);
        Assert.Equal(2, transport.SentFrames.Count);
        // Chunk 1: D100 (wire 0x1100), 4 words
        Assert.Equal(new byte[] { 0x00, 0x00, 0x05, 0x00, 0x1C, 0x00, 0x11, 0x04, 0x00 }, transport.SentFrames[0]);
        // Chunk 2: D104 (wire 0x1104), 2 words
        Assert.Equal(new byte[] { 0x00, 0x00, 0x05, 0x00, 0x1C, 0x04, 0x11, 0x02, 0x00 }, transport.SentFrames[1]);
    }

    [Fact]
    public async Task WriteAsync_Int32MultiChunk_FailureOnSecondChunk_PreservesCompletedCount()
    {
        var transport = new ScriptedTransport();
        // MaxWriteWords = 5 -> wordsPerElement(Int32) = 2 -> maxElementsPerChunk = 2 (4 words per chunk)
        var protocol = new ToyopucProtocol(transport, new ToyopucOptions { MaxWriteWords = 5 });

        transport.EnqueueResponse([0x80, 0x00, 0x01, 0x00, 0x1D]);
        transport.EnqueueResponse([0x80, 0x40, 0x01, 0x00, 0x40]);

        byte[] data = new byte[12]; // 3 Int32 elements
        var request = new PlcWriteRequest("D100", PlcDataType.Int32, data);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.WriteAsync(request, default));

        Assert.Equal(0x40, exception.DeviceErrorCode);
        Assert.Equal(2, exception.CompletedCount);
        Assert.Equal(2, transport.SentFrames.Count);
    }

    [Fact]
    public async Task ReadAsync_ErrorResponse_ThrowsPlcDeviceException()
    {
        var transport = new ScriptedTransport();
        var protocol = new ToyopucProtocol(transport);

        // Error vector from §4: 80 40 01 00 40
        byte[] response = [0x80, 0x40, 0x01, 0x00, 0x40];
        transport.EnqueueResponse(response);

        var request = new PlcReadRequest("D100", PlcDataType.Int16, 1);
        var exception = await Assert.ThrowsAsync<PlcDeviceException>(() => protocol.ReadAsync(request, default));

        Assert.Equal(0x40, exception.DeviceErrorCode);
        Assert.Contains("주소 범위 초과", exception.Message);
    }

    [Fact]
    public void ResponseParser_FourByteZeroBodyFrame_ThrowsInvalidResponse()
    {
        byte[] truncatedFrame = [0x80, 0x00, 0x00, 0x00];

        PlcException writeException = Assert.Throws<PlcException>(() =>
            ToyopucResponseParser.VerifyWriteResponse(truncatedFrame, 0x1D));
        Assert.Equal(PlcErrorKind.InvalidResponse, writeException.Kind);

        PlcException readException = Assert.Throws<PlcException>(() =>
            ToyopucResponseParser.ParseReadWords(truncatedFrame, 0x1C, 1));
        Assert.Equal(PlcErrorKind.InvalidResponse, readException.Kind);
    }
}
