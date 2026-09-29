using System.Text;
using FieldLink.Communication;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Modbus;

namespace FieldLink.Communication.Tests;

internal static partial class SimplePlcTests
{
    internal static async Task ClosedDisposedInvalidInputsAndReadOnlyAreasDoNotSendAsync()
    {
        var closed = Plc.ModbusTcp("127.0.0.1");
        await TestAssert.ThrowsAsync<InvalidOperationException>(() => closed.WriteAsync("0", new[] { 1 }));
        closed.Dispose();
        await TestAssert.ThrowsAsync<ObjectDisposedException>(() => closed.OpenAsync());
        await WithPlcAsync(p => Plc.ModbusTcp("127.0.0.1", p), async (plc, peer) =>
        {
            await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => plc.ReadInt32Async("0", 0));
            TestAssert.True(!(await plc.WriteAsync("65535", 123)).IsSuccess);
            TestAssert.True(!(await plc.WriteAsync("x=4;0", 123)).IsSuccess);
            TestAssert.True(!(await plc.ReadBytesAsync("0", 3)).IsSuccess);
            TestAssert.True(!(await plc.WriteStringAsync("0", "가", 4)).IsSuccess);
            TestAssert.True(!(await plc.WriteStringAsync("0", "ABCDE", 4)).IsSuccess);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await TestAssert.ThrowsAsync<OperationCanceledException>(() => plc.ReadInt16Async("0", cancelled.Token));
            TestAssert.Equal(0, peer.Available);
        });
    }

    internal static Task SplitWriteReportsConfirmedAndRejectedRangesAsync() => WithPlcAsync(
        p => Plc.ModbusTcp("127.0.0.1", p, new ModbusTcpClientOptions { MaxRegistersPerRequest = 2 }), async (plc, peer) =>
        {
            var write = plc.WriteAsync("100", new[] { 1, 2, 3 });
            await ExchangeExpectedAsync(peer, "00010000000B0110006400020400000001", "000100000006011000640002");
            await ExchangeExpectedAsync(peer, "00020000000B0110006600020400000002", "000200000003019002");
            var result = await write;
            TestAssert.True(!result.IsSuccess);
            TestAssert.Equal(2, result.ErrorCode);
            TestAssert.Equal(2, result.FailureDetails.ConfirmedCount);
            TestAssert.Equal(0, result.FailureDetails.UncertainCount);
            TestAssert.Equal("word", result.FailureDetails.Unit);
            TestAssert.Equal(ClientState.Open, plc.State);
            TestAssert.Equal(0, peer.Available);
        });

    internal static Task SplitCancellationReportsUncertainBlockAsync() => WithPlcAsync(
        p => Plc.ModbusTcp("127.0.0.1", p, new ModbusTcpClientOptions { MaxRegistersPerRequest = 2 }), async (plc, peer) =>
        {
            using var cancellation = new CancellationTokenSource();
            var write = plc.WriteAsync("100", new[] { 1, 2, 3 }, cancellation.Token);
            await ExchangeExpectedAsync(peer, "00010000000B0110006400020400000001", "000100000006011000640002");
            await TcpFixture.ReadExactlyAsync(peer, 17);
            cancellation.Cancel();
            var error = await TestAssert.ThrowsAsync<PlcOperationCanceledException>(() => write);
            TestAssert.Equal(2, error.FailureDetails.ConfirmedCount);
            TestAssert.Equal(2, error.FailureDetails.UncertainCount);
            TestAssert.Equal(CommunicationFailure.Cancelled, ((CommunicationException)error.FailureDetails.Cause).Failure);
            TestAssert.Equal(ClientState.Faulted, plc.State);
        });

    internal static Task QueuedCallsCopyInputsAndCancellationPreservesConnectionAsync()
    {
        var options = new ModbusTcpClientOptions { UnitId = 2, ByteOrder = ByteOrder.LittleEndianWithByteSwap };
        return WithPlcAsync(p => Plc.ModbusTcp("127.0.0.1", p, options), async (plc, peer) =>
        {
            options.UnitId = 3; options.ByteOrder = ByteOrder.BigEndian;
            var blocker = plc.ReadInt16Async("0");
            TestAssert.Bytes(H("000100000006020300000001"), await TcpFixture.ReadExactlyAsync(peer, 12));
            using var cancellation = new CancellationTokenSource();
            var cancelled = plc.ReadInt16Async("1", cancellation.Token);
            cancellation.Cancel();
            await TestAssert.ThrowsAsync<OperationCanceledException>(() => cancelled);
            int[] values = { 0x12345678 };
            var write = plc.WriteAsync("10", values);
            values[0] = 0;
            await TcpFixture.WriteAsync(peer, H("0001000000050203020001"));
            TestAssert.True((await blocker).IsSuccess);
            await ExchangeExpectedAsync(peer, "00020000000B0210000A00020456781234", "0002000000060210000A0002");
            TestAssert.True((await write).IsSuccess);
            TestAssert.Equal(ClientState.Open, plc.State);
        });
    }

    internal static Task SplitRequestsShareOneDeadlineAsync() => WithPlcAsync(
        p => Plc.ModbusTcp("127.0.0.1", p, new ModbusTcpClientOptions { Timeout = TimeSpan.FromMilliseconds(800), MaxRegistersPerRequest = 1 }), async (plc, peer) =>
        {
            var read = plc.ReadInt32Async("0");
            await TcpFixture.ReadExactlyAsync(peer, 12);
            await Task.Delay(500);
            await TcpFixture.WriteAsync(peer, H("0001000000050103020001"));
            await TcpFixture.ReadExactlyAsync(peer, 12);
            TestAssert.True(await Task.WhenAny(read, Task.Delay(600)) == read);
            var result = await read;
            TestAssert.True(!result.IsSuccess);
            TestAssert.Equal(1, result.FailureDetails.ConfirmedCount);
            TestAssert.Equal(CommunicationFailure.Timeout, ((CommunicationException)result.FailureDetails.Cause).Failure);
        });

    internal static Task ModbusStringsAndCoilsUseExplicitWireLengthsAsync() => WithPlcAsync(
        p => Plc.ModbusTcp("127.0.0.1", p), async (plc, peer) =>
        {
            var text = plc.WriteStringAsync("10", "가", 4, Encoding.UTF8);
            await ExchangeExpectedAsync(peer, "00010000000B0110000A000204EAB08000", "0001000000060110000A0002");
            TestAssert.True((await text).IsSuccess);
            var read = plc.ReadStringAsync("10", 4, Encoding.UTF8);
            await ExchangeExpectedAsync(peer, "0002000000060103000A0002", "000200000007010304EAB08000");
            TestAssert.Equal("가", (await read).Content);
            var bits = plc.WriteAsync("0", new[] { true, false, true, true, false, false, false, false, true });
            await ExchangeExpectedAsync(peer, "000300000009010F00000009020D01", "000300000006010F00000009");
            TestAssert.True((await bits).IsSuccess);
            var bitRead = plc.ReadBoolAsync("0", 9);
            await ExchangeExpectedAsync(peer, "000400000006010100000009", "0004000000050101020D01");
            TestAssert.True((await bitRead).Content.SequenceEqual(new[] { true, false, true, true, false, false, false, false, true }));
        });

    internal static Task WrongPayloadIsRejectedAndUnmatchedIdIsIgnoredAsync() => WithPlcAsync(
        p => Plc.ModbusTcp("127.0.0.1", p), async (plc, peer) =>
        {
            var read = plc.ReadInt16Async("0");
            await TcpFixture.ReadExactlyAsync(peer, 12);
            await TcpFixture.WriteAsync(peer, H("00FF0000000501030200050001000000050103020007"));
            TestAssert.Equal((short)7, (await read).Content);
            var broken = plc.ReadInt32Async("0");
            await TcpFixture.ReadExactlyAsync(peer, 12);
            await TcpFixture.WriteAsync(peer, H("0002000000050103020001"));
            var result = await broken;
            TestAssert.True(!result.IsSuccess);
            TestAssert.Equal(CommunicationFailure.ResponseRejected, ((CommunicationException)result.FailureDetails.Cause).Failure);
            TestAssert.Equal(ClientState.Faulted, plc.State);
        });
}
