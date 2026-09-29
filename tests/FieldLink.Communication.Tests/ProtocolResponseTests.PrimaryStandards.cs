using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Modbus;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    // Mitsubishi SLMP SH-080956ENG-N, §4.2 and §5.2 (T100..T102 example).
    internal static Task McStandardsBinaryResponseRequiresHeaderAndExactLengthAsync()
    {
        byte[] valid = H("D00000FFFF03000800000034120200EF1D");
        TestAssert.True(McBinaryResponseParser.CheckResponseContentHelper(valid).IsSuccess);
        byte[] wrongHeader = (byte[])valid.Clone();
        wrongHeader[0] = 0x50;
        Failed(McBinaryResponseParser.CheckResponseContentHelper(wrongHeader));
        valid[7] = 7;
        Failed(McBinaryResponseParser.CheckResponseContentHelper(valid));
        Failed(McBinaryResponseParser.CheckResponseContentHelper(H("D00000FFFF030002005BC0")), 0xC05B);
        return Task.CompletedTask;
    }

    internal static Task McStandardsAsciiResponseRequiresHeaderAndExactLengthAsync()
    {
        TestAssert.True(McAsciiResponseParser.CheckResponseContent(A("D00000FF03FF0000100000123400021DEF")).IsSuccess);
        Failed(McAsciiResponseParser.CheckResponseContent(A("500000FF03FF0000040000")));
        Failed(McAsciiResponseParser.CheckResponseContent(A("D00000FF03FF0000050000")));
        Failed(McAsciiResponseParser.CheckResponseContent(A("D00000FF03FF000004C05B")), 0xC05B);
        return Task.CompletedTask;
    }

    internal static Task McStandardsAsciiMalformedFieldsReturnFailureAsync()
    {
        Failed(McAsciiResponseParser.CheckResponseContent(A("D00000FF03FF000004ZZZZ")));
        Failed(McAsciiResponseParser.CheckResponseContent(A("D00000FF03FF00ZZZZ0000")));
        return Task.CompletedTask;
    }

    internal static Task McStandardsReadMatchesPublishedExampleAsync()
    {
        var address = McDeviceAddress.ParseMelsecFrom("TN100", 3, false).Content;
        TestAssert.Bytes(H("01040000640000C20300"), McBinaryCommandBuilder.BuildReadMcCoreCommand(address, false));
        TestAssert.Bytes(A("04010000TN0001000003"), McAsciiCommandBuilder.BuildAsciiReadMcCoreCommand(address, false));
        return Task.CompletedTask;
    }

    internal static async Task McStandardsWordWriteRejectsHalfWordAsync()
    {
        var address = McDeviceAddress.ParseMelsecFrom("D100", 1, false).Content;
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(
            McBinaryCommandBuilder.BuildWriteWordCoreCommand(address, new byte[] { 0x34 })));
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(
            McAsciiCommandBuilder.BuildAsciiWriteWordCoreCommand(address, new byte[] { 0x34 })));
        TestAssert.Bytes(H("01140000640000A801003412"), McBinaryCommandBuilder.BuildWriteWordCoreCommand(address, H("3412")));
        TestAssert.Bytes(A("14010000D*00010000011234"), McAsciiCommandBuilder.BuildAsciiWriteWordCoreCommand(address, H("3412")));
    }

    internal static async Task McStandardsFrameLengthCannotWrapAsync()
    {
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            McBinaryCommandBuilder.PackMcCommand(new McFrameOptions(), new byte[65534])));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            McAsciiCommandBuilder.PackMcCommand(new McFrameOptions(), new byte[65532])));
    }

    internal static async Task McStandardsBatchRequestsRespectPointLimitsAsync()
    {
        var address = McDeviceAddress.ParseMelsecFrom("D100", 0, false).Content;
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            McBinaryCommandBuilder.BuildReadMcCoreCommand(address, false)));
        address.Length = 961;
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            McAsciiCommandBuilder.BuildAsciiReadMcCoreCommand(address, false)));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            McBinaryCommandBuilder.BuildWriteWordCoreCommand(address, new byte[1922])));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            McAsciiCommandBuilder.BuildAsciiWriteWordCoreCommand(address, new byte[0])));
        address = McDeviceAddress.ParseMelsecFrom("M0", 7169, true).Content;
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            McBinaryCommandBuilder.BuildReadMcCoreCommand(address, true)));
        address.Length = 3585;
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            McAsciiCommandBuilder.BuildAsciiReadMcCoreCommand(address, true)));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            McBinaryCommandBuilder.BuildWriteBitCoreCommand(address, new bool[7169])));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            McAsciiCommandBuilder.BuildAsciiWriteBitCoreCommand(address, new bool[3585])));
        address.Length = 960;
        TestAssert.Equal(10, McBinaryCommandBuilder.BuildReadMcCoreCommand(address, false).Length);
        TestAssert.Equal(20, McAsciiCommandBuilder.BuildAsciiReadMcCoreCommand(address, false).Length);
        TestAssert.Equal(1930, McBinaryCommandBuilder.BuildWriteWordCoreCommand(address, new byte[1920]).Length);
        TestAssert.Equal(3860, McAsciiCommandBuilder.BuildAsciiWriteWordCoreCommand(address, new byte[1920]).Length);
        TestAssert.Equal(3594, McBinaryCommandBuilder.BuildWriteBitCoreCommand(address, new bool[7168]).Length);
        TestAssert.Equal(3604, McAsciiCommandBuilder.BuildAsciiWriteBitCoreCommand(address, new bool[3584]).Length);
    }

    // Modbus Application Protocol V1.1b3 §4.1 (253-byte PDU) and §6.1/6.2.
    internal static async Task ModbusStandardsTcpBuilderEnforcesAduLimitAsync()
    {
        TestAssert.Equal(260, ModbusFrameRules.PackCommandToTcp(new byte[254], 0x1234).Length);
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            ModbusFrameRules.PackCommandToTcp(new byte[255], 1)));
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(
            ModbusFrameRules.PackCommandToTcp(new byte[1], 1)));
    }

    internal static Task ModbusStandardsReadBitsRejectsNonzeroPaddingAsync()
    {
        // Official §6.1 example: 19 outputs, last byte has 3 used bits.
        Payload(ModbusResponseParser.Parse(H("010100130013"), H("010103CD6B05")), "CD6B05");
        Failed(ModbusResponseParser.Parse(H("010100130013"), H("010103CD6B85")));
        Payload(ModbusResponseParser.Parse(H("010200C40016"), H("010203ACDB35")), "ACDB35");
        Failed(ModbusResponseParser.Parse(H("010200C40016"), H("010203ACDBF5")));
        Payload(ModbusResponseParser.Parse(H("010100000008"), H("010101FF")), "FF");
        return Task.CompletedTask;
    }
}
