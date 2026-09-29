using FieldLink.PlcDrivers.LSIS;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    // LS ELECTRIC XGL-EFMTB V3.5 §7.1.1; XGT FEnet V2.30 §§8.1.2, 8.2.1–8.2.4.
    // Fixed application fields come from the manual; no product builder creates these responses.
    internal static Task LsStandardsRejectsMalformedEnvelopeAsync()
    {
        byte[] valid = LsStandardsFrame("5500140000000000010004003412CDAB");
        Payload(LSFastEnetResponseParser.ExtractActualData(new FastEnetFrameOptions(), valid), "3412CDAB");
        foreach (int offset in new[] { 0, 13, 16, 21, 23 })
        {
            byte[] malformed = (byte[])valid.Clone();
            malformed[offset] ^= 1;
            Failed(LSFastEnetResponseParser.ExtractActualData(new FastEnetFrameOptions(), malformed));
        }
        return Task.CompletedTask;
    }

    internal static Task LsStandardsRequiresCompleteWriteAcknowledgementAsync()
    {
        var options = new FastEnetFrameOptions();
        Payload(LSFastEnetResponseParser.ExtractActualData(options,
            LsStandardsFrame("59001400000000000100")), "");
        Failed(LSFastEnetResponseParser.ExtractActualData(options,
            LsStandardsFrame("5900140000000000"))); // Missing block count.
        Failed(LSFastEnetResponseParser.ExtractActualData(options,
            LsStandardsFrame("59001400000000000000"))); // No block acknowledged.
        Failed(LSFastEnetResponseParser.ExtractActualData(options,
            LsStandardsFrame("59001400000000000100FF"))); // Trailing data.
        return Task.CompletedTask;
    }

    internal static Task LsStandardsPreservesSixteenBitErrorAsync()
    {
        var options = new FastEnetFrameOptions();
        Failed(LSFastEnetResponseParser.ExtractActualData(options,
            LsStandardsFrame("590014000000FFFF9001")), 0x190);
        Failed(LSFastEnetResponseParser.ExtractActualData(options,
            LsStandardsFrame("590014000000FFFF21")), 0x21);
        Failed(LSFastEnetResponseParser.ExtractActualData(options,
            LsStandardsFrame("590014000000FFFF2100")), 0x21);
        return Task.CompletedTask;
    }

    internal static Task LsStandardsReadsEveryDeclaredBlockAsync()
    {
        var options = new FastEnetFrameOptions();
        Payload(LSFastEnetResponseParser.ExtractActualData(options,
            LsStandardsFrame("55000200000000000200020034120200CDAB")), "3412CDAB");
        foreach (string application in new[]
        {
            "5500020000000000020002003412", // Missing second block.
            "5500140000000000010004003412", // Truncated payload.
            "5500140000000000010002003412FF", // Trailing payload.
            "55001400000000000200020034120200CDAB" // Continuous reads have one block.
        })
            Failed(LSFastEnetResponseParser.ExtractActualData(options, LsStandardsFrame(application)));
        return Task.CompletedTask;
    }

    internal static Task LsStandardsReportsXgiCpuTypeAsync()
    {
        var options = new FastEnetFrameOptions();
        byte[] response = LsStandardsFrame("59001400000000000100");
        response[10] = 5;
        Payload(LSFastEnetResponseParser.ExtractActualData(options, response), "");
        TestAssert.Equal("XGK/I-CPUU", options.CpuType);
        return Task.CompletedTask;
    }

    private static byte[] LsStandardsFrame(string application)
    {
        byte[] body = H(application);
        byte[] frame = new byte[20 + body.Length];
        H("4C5349532D58475400000101A011341200000000").CopyTo(frame, 0);
        frame[16] = (byte)body.Length;
        frame[17] = (byte)(body.Length >> 8);
        body.CopyTo(frame, 20);
        return frame;
    }
}
