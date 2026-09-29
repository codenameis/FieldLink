using System.Text;
using FieldLink.Secs;
using FieldLink.Secs.Protocols;
using FieldLink.Secs.Types;

namespace FieldLink.Communication.Tests;

internal static partial class SecsTests
{
    internal static Task ReferenceItemAndHsmsPacketsAsync()
    {
        // Reference Secs2 format constants and Secs1.BuildHSMSMessage, independently fixed bytes.
        var item = new SecsValue(new object[] { "AB", (ushort)0x1234, new byte[] { 0, 255 }, true });
        byte[] expected = TestBytes.FromHexString("010441024142A9021234210200FF2501FF");
        TestAssert.Bytes(expected, item.ToSourceBytes(Encoding.ASCII));
        var message = new SecsMessage(0x8123, 1, 1, 0x12345678, expected, true);
        byte[] packet = TestBytes.FromHexString("0000001B81238101000012345678010441024142A9021234210200FF2501FF");
        TestAssert.Bytes(packet, HsmsCodec.Encode(message));
        var decoded = HsmsCodec.Decode(packet);
        TestAssert.Equal((ushort)0x8123, decoded.DeviceID);
        TestAssert.Equal(0x12345678u, decoded.MessageID);
        TestAssert.True(decoded.W);
        TestAssert.Bytes(expected, decoded.GetItemValues(Encoding.ASCII).ToSourceBytes(Encoding.ASCII));
        return Task.CompletedTask;
    }

    internal static async Task InvalidItemsAndHeadersAreRejectedAsync()
    {
        foreach (string hex in new[] { "01", "00", "7103000000", "FD00", "410541", "0102410141", "01000100" })
            await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(SecsValue.ParseFromSource(TestBytes.FromHexString(hex), Encoding.ASCII)));
        foreach (string hex in new[] { "00000009", "FFFFFFFF", "0000000A000000000000000000" })
            await TestAssert.ThrowsAsync<InvalidDataException>(() => Task.FromResult(HsmsCodec.Decode(TestBytes.FromHexString(hex))));
    }
}
