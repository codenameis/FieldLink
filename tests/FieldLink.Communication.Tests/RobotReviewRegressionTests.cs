using System.Globalization;
using FieldLink.Robot.FANUC.Protocols;
using FieldLink.Robot.YASKAWA.Protocols;
using FieldLink.Robot.ABB.Protocols;
using FieldLink.Robot.ABB.Clients;

namespace FieldLink.Communication.Tests;

internal static partial class RobotProtocolTests
{
    internal static Task ReviewYrcJobSelectionMatchesOfficialFieldsAsync()
    {
        // Yaskawa Job Selection and Start: 0x87, Instance 1, Attribute 0, Service 2.
        byte[] packet = new YrcEthernetRequestBuilder().BuildJSeq("MAIN", 12).Build(7);
        TestAssert.Bytes(TestBytes.FromHexString("8700010000020000"), TestBytes.Slice(packet, 24, 32));
        TestAssert.Equal((byte)7, packet[11]);
        TestAssert.Equal("MAIN", System.Text.Encoding.ASCII.GetString(packet, 32, 4));
        TestAssert.Bytes([12, 0, 0, 0], TestBytes.Slice(packet, 64, 68));
        return Task.CompletedTask;
    }

    internal static Task ReviewFanucToolOnlyWritePreservesUserFrameAsync()
    {
        foreach (short frame in new short[] { -1, 16 })
        {
            var commands = FanucProtocol.BuildWriteJoint(100, new float[9], frame, 7);
            byte[] tool = commands.Last();
            TestAssert.Equal((ushort)145, BitConverter.ToUInt16(tool, 44)); // 1 기반 100 + 46의 wire 주소.
            TestAssert.Equal((ushort)1, BitConverter.ToUInt16(tool, 46));
            TestAssert.Bytes([7, 0], TestBytes.Slice(tool, 48, 50));
            TestAssert.Equal(3, commands.Length);
        }
        var both = FanucProtocol.BuildWriteJoint(100, new float[9], 2, 7).Last();
        TestAssert.Equal((ushort)143, BitConverter.ToUInt16(both, 44));
        TestAssert.Bytes([0, 0, 2, 0, 7, 0], TestBytes.Slice(both, 48, 54));
        return Task.CompletedTask;
    }

    internal static Task ReviewYrcNumbersUseInvariantWireCultureAsync()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            foreach (string name in new[] { "ko-KR", "de-DE", "fr-FR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                var pose = new YrcRobotPosition { X = 1.5f, Y = -2.25f };
                TestAssert.Equal("1.5", pose.ToWriteString(YrcControllerModel.YRC100).Split(',')[2]);
                pose.Parse(YrcControllerModel.YRC100, "1.5,-2.25,0,0,0,0,0,0,1");
                TestAssert.Equal(1.5f, pose.X);
                TestAssert.Equal(-2.25f, pose.Y);
            }
        }
        finally { CultureInfo.CurrentCulture = saved; }
        return Task.CompletedTask;
    }
}

internal static partial class RobotClientTests
{
    internal static async Task ReviewAbbLogSendsResourceQueryAsync()
    {
        using var handler = new AbbHandler();
        using var http = new HttpClient(handler);
        var robot = new AbbHttpClient(http, new Uri("http://localhost:8123/"));
        await robot.ExecuteAsync(AbbRequestBuilder.GetLog());
        TestAssert.Equal("/rw/elog/0?lang=zh&resource=title", handler.LastPath!);
    }
}
