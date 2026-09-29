using FieldLink.PlcDrivers.Beckhoff;
using FieldLink.PlcDrivers.YASKAWA;
using FieldLink.PlcDrivers.Yokogawa;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task AdsStandardsRejectsIncompleteAndInconsistentRepliesAsync()
    {
        // Beckhoff Infosys: AMS/TCP Header, AMS Header, ADS Read/Write Response.
        var options = new AdsFrameOptions();
        byte[] response = H("00002C0000000708090A0B0C01800102030405065303020005000C000000000000007856341200000000040000003412CDAB");
        Payload(BeckhoffAdsNetResponseParser.UnpackResponseContent(options, [], response), "3412CDAB");
        foreach (byte[] malformed in new[] { Array.Empty<byte>(), new byte[37],
            H("0000200000000000000000000000000000000000000003000500000000000000000000000000") })
            Failed(BeckhoffAdsNetResponseParser.UnpackResponseContent(options, [], malformed));
        foreach (int offset in new[] { 0, 2, 24, 26, 42 })
        {
            byte[] malformed = (byte[])response.Clone();
            malformed[offset] ^= 1;
            Failed(BeckhoffAdsNetResponseParser.UnpackResponseContent(options, [], malformed));
        }
        return Task.CompletedTask;
    }

    internal static Task AdsStandardsCorrelatesInvokeCommandAndEndpointsAsync()
    {
        byte[] request = H("00002C00000001020304050653030708090A0B0C0180020004000C0000000000000078563412204000006400000004000000");
        byte[] response = H("00002C0000000708090A0B0C01800102030405065303020005000C000000000000007856341200000000040000003412CDAB");
        var options = new AdsFrameOptions();
        Payload(BeckhoffAdsNetResponseParser.UnpackResponseContent(options, request, response), "3412CDAB");
        foreach (int offset in new[] { 6, 14, 22, 34 })
        {
            byte[] malformed = (byte[])response.Clone();
            malformed[offset] ^= 1;
            Failed(BeckhoffAdsNetResponseParser.UnpackResponseContent(options, request, malformed));
        }
        return Task.CompletedTask;
    }

    internal static Task YokogawaStandardsRequiresCompleteBinaryHeaderAndLengthAsync()
    {
        // IM34M06H24-08E §5.3.2, binary response: subheader, exit code, BE size, parameters.
        Payload(YokogawaLinkTcpResponseParser.CheckContent(H("910000041234ABCD")), "1234ABCD");
        foreach (string malformed in new[] { "9100", "910000", "910000051234ABCD", "910000031234ABCD", "110000041234ABCD" })
            Failed(YokogawaLinkTcpResponseParser.CheckContent(H(malformed)));
        return Task.CompletedTask;
    }

    internal static Task MemobusStandardsValidatesBothLengthsAndResponseIdentityAsync()
    {
        // SIEP C880700 04Q Appendix B.1/B.2 and E.1: 218 header, MFC=20, SFC=09.
        byte[] request = H("11210000000016000000000008002009000000000200");
        byte[] response = H("1921000000001800000000000A002009000002003412CDAB");
        Payload(MemobusResponseParser.UnpackResponseContent(request, response), "0A002009000002003412CDAB");
        foreach (int offset in new[] { 0, 1, 2, 3, 6, 12, 14 })
        {
            byte[] malformed = (byte[])response.Clone();
            malformed[offset] ^= 1;
            Failed(MemobusResponseParser.UnpackResponseContent(request, malformed));
        }
        Failed(MemobusResponseParser.UnpackResponseContent(request, new byte[15]));
        Failed(MemobusResponseParser.UnpackResponseContent(request,
            H("192100000000120000000000040020890002")), 2);
        return Task.CompletedTask;
    }
}
