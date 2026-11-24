using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;
using Xunit;

namespace FieldLink.Robot.Tests.Conversion;

public sealed class RobotConversionTests
{
    [Fact]
    public void ReadSingleWordSwapBigEndian_DecodesSpecVectorCdabFloat()
    {
        // 출처: docs/specs/robot/03-estun-tcp.md §9.2 Vector 3 (AO[0]=1.0f CDAB = 00 00 3F 80)
        byte[] wireBytes = new byte[] { 0x00, 0x00, 0x3F, 0x80 };
        float value = RobotBinaryBuffer.ReadSingleWordSwapBigEndian(wireBytes, 0);

        Assert.Equal(1.0f, value);
    }

    [Fact]
    public void ReverseByWord_SwapsEach16BitWordWithoutMutatingSource()
    {
        byte[] source = new byte[] { 0x42, 0x41, 0x44, 0x43 }; // "BADC"
        byte[] swapped = RobotBinaryBuffer.ReverseByWord(source, 0, source.Length);

        Assert.Equal("ABCD", Encoding.ASCII.GetString(swapped));
        Assert.Equal(0x42, source[0]);
    }

    [Fact]
    public void BitPacking_PackAndUnpack_FollowsLsbFirstOrder()
    {
        bool[] bits = new bool[10];
        bits[0] = true;
        bits[3] = true;
        bits[9] = true;

        byte[] packed = RobotBitPacking.Pack(bits);
        Assert.Equal(new byte[] { 0x09, 0x02 }, packed);

        bool[] unpacked = RobotBitPacking.Unpack(packed, 0, 10);
        Assert.Equal(bits, unpacked);
    }

    [Fact]
    public void RobotJsonWriter_EscapesStringsAndFormatsInvariantNumbers()
    {
        var dict = new Dictionary<string, string>
        {
            ["key"] = "line1\n\"quoted\"\\end"
        };

        string jsonDict = RobotJsonWriter.SerializeStringDictionary(dict);
        Assert.Equal("{\"key\":\"line1\\n\\\"quoted\\\"\\\\end\"}", jsonDict);

        string jsonArray = RobotJsonWriter.SerializeDoubleArray(new[] { 10.5, -20.25, 9e9 });
        Assert.Equal("[10.5,-20.25,9000000000]", jsonArray);
    }

    [Fact]
    public void RobotResult_SuccessAndFailure_PreserveStateAndCopyRawFrame()
    {
        byte[] frame = new byte[] { 0xAA, 0xBB };
        var error = new RobotError(RobotErrorKind.DeviceError, "Rejected", deviceErrorCode: 0x2010, rawFrame: frame);
        frame[0] = 0x00;

        RobotResult<int> failure = RobotResult.Failure<int>(error);
        Assert.False(failure.IsSuccess);
        Assert.Equal(0x2010, failure.Error.DeviceErrorCode);
        Assert.Equal(new byte[] { 0xAA, 0xBB }, failure.Error.GetRawFrame());
        Assert.Throws<InvalidOperationException>(() => failure.Value);

        RobotResult<int> success = RobotResult.Success(42);
        Assert.True(success.IsSuccess);
        Assert.Equal(42, success.Value);
    }

    [Fact]
    public async Task RobotErrorMapper_DistinguishesTimeoutVsCallerCancellation()
    {
        RobotResult timeoutResult = await RobotErrorMapper.ExecuteAsync(
            async token => await Task.Delay(500, token).ConfigureAwait(false),
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.False(timeoutResult.IsSuccess);
        Assert.Equal(RobotErrorKind.Timeout, timeoutResult.Error.Kind);

        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            RobotErrorMapper.ExecuteAsync(
                token => Task.FromCanceled(token),
                TimeSpan.FromSeconds(5),
                callerCts.Token));

        RobotResult transportTimeout = await RobotErrorMapper.ExecuteAsync(
            _ => throw new TransportException(
                TransportFailure.ReceiveFailed,
                "Timed out",
                isRequestSent: true,
                innerException: new TimeoutException()),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(transportTimeout.IsSuccess);
        Assert.Equal(RobotErrorKind.Timeout, transportTimeout.Error.Kind);
    }

    [Fact]
    public void BinaryBufferAndBitPacking_GuardBoundsAndPreventOverflow()
    {
        Assert.Throws<ArgumentNullException>(() => RobotBinaryBuffer.ReverseByWord(null!, 0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => RobotBinaryBuffer.ReverseByWord(new byte[4], 0, -2));
        Assert.Throws<ArgumentOutOfRangeException>(() => RobotBinaryBuffer.ReverseByWord(new byte[4], 0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => RobotBinaryBuffer.ReverseByWord(new byte[4], -1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => RobotBinaryBuffer.ReverseByWord(new byte[4], 2, 4));
        Assert.Equal(new byte[] { 0x34, 0x12 }, RobotBinaryBuffer.ReverseByWord(new byte[] { 0x00, 0x12, 0x34, 0x56 }, 1, 2));

        Assert.Equal(0, RobotBitPacking.GetPackedByteCount(0));
        Assert.Equal(268_435_456, RobotBitPacking.GetPackedByteCount(int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => RobotBitPacking.GetPackedByteCount(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RobotBitPacking.Unpack(new byte[2], int.MaxValue, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => RobotBitPacking.Unpack(new byte[2], 0, -1));
    }
}
