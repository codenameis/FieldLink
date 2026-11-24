using System;
using System.Threading.Tasks;
using FieldLink.Robot.Protocols.Kuka.SocketAscii;
using FieldLink.Robot.Tests.Fakes;
using Xunit;

namespace FieldLink.Robot.Tests.Protocols.Kuka.SocketAscii;

public sealed class KukaSocketAsciiTests
{
    [Fact]
    public void RequestBuilder_SpecVectorsAndMismatchedLengths_BehaveAsSpecified()
    {
        // 출처: docs/specs/robot/06-kuka.md §9.2
        Assert.Equal(
            FakeTransport.ParseHex("30 30 24 4F 56 5F 50 52 4F"),
            KukaSocketAsciiRequestBuilder.BuildReadRequest(new[] { "$OV_PRO" }));

        Assert.Equal(
            FakeTransport.ParseHex("30 31 41 3D 31 2C 42 3D 32"),
            KukaSocketAsciiRequestBuilder.BuildWriteRequest(new[] { "A", "B" }, new[] { "1", "2" }));

        Assert.Equal(
            FakeTransport.ParseHex("30 36 32 31"),
            KukaSocketAsciiRequestBuilder.BuildStopProgramRequest());

        Assert.Throws<ArgumentException>(
            () => KukaSocketAsciiRequestBuilder.BuildWriteRequest(new[] { "A", "B" }, new[] { "1" }));
    }

    [Fact]
    public async Task Client_ReadControlAndCaseSensitiveErrCheck_WorkCorrectly()
    {
        var transport = new FakeTransport();
        transport.EnqueueIncomingAscii("50");
        transport.EnqueueIncomingAscii("OK");
        transport.EnqueueIncomingAscii("ERR_IGNORED_UPPERCASE");
        transport.EnqueueIncomingAscii("err: invalid var");

        using var client = new KukaSocketAsciiClient(transport);
        await client.OpenAsync();

        RobotResult<string> read = await client.ReadVariablesAsync(new[] { "$OV_PRO" });
        Assert.True(read.IsSuccess);
        Assert.Equal("50", read.Value);

        RobotResult writeOk = await client.WriteVariablesAsync(new[] { "$OV_PRO" }, new[] { "80" });
        Assert.True(writeOk.IsSuccess);

        RobotResult upperOk = await client.ResetProgramAsync();
        Assert.True(upperOk.IsSuccess);

        RobotResult lowerErr = await client.StopProgramAsync();
        Assert.False(lowerErr.IsSuccess);
        Assert.Equal(RobotErrorKind.DeviceError, lowerErr.Error.Kind);
        Assert.Contains("err: invalid var", lowerErr.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestBuilderAndClient_AllowCommasInWriteValuesWhileRejectingDelimiterInjection()
    {
        var transport = new FakeTransport();
        transport.EnqueueIncomingAscii("OK");

        using var client = new KukaSocketAsciiClient(transport);
        await client.OpenAsync();

        const string KrlStruct = "{E6POS: X 100.0, Y 200.0, Z 300.0}";
        RobotResult writeStruct = await client.WriteVariablesAsync(new[] { "MY_POS" }, new[] { KrlStruct });
        Assert.True(writeStruct.IsSuccess);
        Assert.Equal(
            System.Text.Encoding.ASCII.GetBytes($"01MY_POS={KrlStruct}"),
            transport.SentFrames[0]);

        Assert.Throws<ArgumentException>(() => KukaSocketAsciiRequestBuilder.BuildWriteRequest(new[] { "A,B" }, new[] { "1" }));
        Assert.Throws<ArgumentException>(() => KukaSocketAsciiRequestBuilder.BuildWriteRequest(new[] { "A=B" }, new[] { "1" }));
        Assert.Throws<ArgumentException>(() => KukaSocketAsciiRequestBuilder.BuildWriteRequest(new[] { "A\r\n" }, new[] { "1" }));
        Assert.Throws<ArgumentException>(() => KukaSocketAsciiRequestBuilder.BuildWriteRequest(new[] { "A" }, new[] { "1\n2" }));
        Assert.Throws<ArgumentException>(() => KukaSocketAsciiRequestBuilder.BuildReadRequest(new[] { "A\nB" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.StartProgramAsync("MAIN\r\n"));
    }
}
