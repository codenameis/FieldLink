using FieldLink.PlcDrivers.Common;

namespace FieldLink.Communication.Tests;

internal static class ProtocolCommonTests
{
    internal static Task ConversionChainStopsAtEveryFailureAndPreservesDetailsAsync()
    {
        for (int failedStage = 0; failedStage <= 10; failedStage++)
        {
            var cause = new InvalidOperationException("conversion rejected");
            var details = new OperationFailureDetails("D100", "Read", "word", 8, 0, cause);
            var failure = new OperationResult<int>(913, "stage rejected") { FailureDetails = details };
            int called = 0;
            Func<int, OperationResult<int>> Stage(int stage) => value =>
            {
                called++;
                TestAssert.Equal(stage - 1, value);
                return stage == failedStage ? failure : OperationResult.CreateSuccessResult(value + 1);
            };
            var result = ProtocolConversions.GetResultFromOther(
                failedStage == 0 ? failure : OperationResult.CreateSuccessResult(0),
                Stage(1), Stage(2), Stage(3), Stage(4), Stage(5),
                Stage(6), Stage(7), Stage(8), Stage(9), Stage(10));
            TestAssert.True(!result.IsSuccess, $"Failure at stage {failedStage} must stop the chain");
            TestAssert.Equal(failedStage, called);
            TestAssert.Equal(913, result.ErrorCode);
            TestAssert.Equal("stage rejected", result.Message);
            TestAssert.True(ReferenceEquals(details, result.FailureDetails));
        }
        return Task.CompletedTask;
    }

    internal static Task ConversionChainPreservesSuccessfulValuesAsync()
    {
        Func<int, OperationResult<int>> next = value => OperationResult.CreateSuccessResult(value + 1);
        var result = ProtocolConversions.GetResultFromOther(OperationResult.CreateSuccessResult(0),
            next, next, next, next, next, next, next, next, next, next);
        TestAssert.True(result.IsSuccess);
        TestAssert.Equal(10, result.Content);
        return Task.CompletedTask;
    }

    internal static Task ValueConversionFailureRetainsOriginalExceptionAsync()
    {
        var cause = new FormatException("invalid value");
        var result = ProtocolConversions.GetSuccessResultFromOther<int, string>(
            OperationResult.CreateSuccessResult("bad"), _ => throw cause);
        TestAssert.True(!result.IsSuccess);
        TestAssert.True(ReferenceEquals(cause, result.FailureDetails?.Cause));
        TestAssert.True(result.Message.Contains(cause.Message));
        return Task.CompletedTask;
    }

    internal static Task ByteConversionFailureRetainsOriginalExceptionAsync()
    {
        var cause = new FormatException("short payload");
        var result = ProtocolConversions.GetResultFromBytes<int>(
            OperationResult.CreateSuccessResult(new byte[] { 0xAB }), _ => throw cause);
        TestAssert.True(!result.IsSuccess);
        TestAssert.True(ReferenceEquals(cause, result.FailureDetails?.Cause));
        TestAssert.True(result.Message.Contains("AB") && result.Message.Contains("Length(1)"));
        return Task.CompletedTask;
    }

    internal static Task NullBytePayloadDoesNotHideOriginalConversionExceptionAsync()
    {
        var cause = new FormatException("missing payload");
        var result = ProtocolConversions.GetResultFromBytes<int>(
            OperationResult.CreateSuccessResult<byte[]>(null!), _ => throw cause);
        TestAssert.True(!result.IsSuccess);
        TestAssert.True(ReferenceEquals(cause, result.FailureDetails?.Cause));
        return Task.CompletedTask;
    }

    internal static Task FailedConversionsDoNotInvokeTranslatorAsync()
    {
        var details = new OperationFailureDetails("D0", "Read", "word", 0, 0, new IOException("closed"));
        var failure = new OperationResult<byte[]>(121, "device rejected") { FailureDetails = details };
        var bytes = ProtocolConversions.GetResultFromBytes<int>(failure, _ => throw new Exception("Must not run"));
        var value = ProtocolConversions.GetSuccessResultFromOther<int, byte[]>(failure, _ => throw new Exception("Must not run"));
        foreach (var result in new[] { bytes, value })
        {
            TestAssert.True(!result.IsSuccess);
            TestAssert.Equal(121, result.ErrorCode);
            TestAssert.Equal(failure.Message, result.Message);
            TestAssert.True(ReferenceEquals(details, result.FailureDetails));
        }
        return Task.CompletedTask;
    }

    internal static async Task ArraySplitRejectsNonPositiveLengthAsync()
    {
        // Check a bounded invalid input first: the unfixed zero case would allocate forever.
        await OutOfRangeAsync(() => ProtocolBytes.ArraySplitByLength(new[] { 1 }, -1), "length");
        await OutOfRangeAsync(() => ProtocolBytes.ArraySplitByLength(new[] { 1 }, 0), "length");
    }

    internal static async Task IntegerSplitRejectsInvalidLengthsAsync()
    {
        await OutOfRangeAsync(() => ProtocolBytes.SplitIntegerToArray(-1, 1), "integer");
        await OutOfRangeAsync(() => ProtocolBytes.SplitIntegerToArray(1, -1), "everyLength");
        await OutOfRangeAsync(() => ProtocolBytes.SplitIntegerToArray(1, 0), "everyLength");
        await OutOfRangeAsync(() => AddressParameters.SplitReadLength(100, 1, 0), "segment");
    }

    internal static async Task WriteSplitRejectsInvalidUnitsBeforeSplittingAsync()
    {
        // The negative case is finite even before the guard is implemented.
        await OutOfRangeAsync(() => AddressParameters.SplitWriteData(100, new[] { 1 }, 1, -1), "addressLength");
        await OutOfRangeAsync(() => AddressParameters.SplitWriteData(100, new[] { 1 }, 0, 1), "segment");
        await OutOfRangeAsync(() => AddressParameters.SplitWriteData(100, new[] { 1 }, 1, 0), "addressLength");
        await OutOfRangeAsync(() => AddressParameters.SplitWriteData(100, Array.Empty<int>(), ushort.MaxValue, int.MaxValue), "addressLength");
    }

    internal static Task SplittingPreservesDataUnitsEmptyInputsAndIndependentArraysAsync()
    {
        byte[] source = [1, 2, 3, 4, 5, 6];
        var chunks = ProtocolBytes.ArraySplitByLength(source, 4);
        TestAssert.Equal(2, chunks.Count);
        TestAssert.Bytes([1, 2, 3, 4], chunks[0]);
        TestAssert.Bytes([5, 6], chunks[1]);
        chunks[0][0] = 99;
        TestAssert.Equal((byte)1, source[0]);
        TestAssert.Bytes(source, ProtocolBytes.ArraySplitByLength(source, int.MaxValue)[0]);
        TestAssert.Equal(0, ProtocolBytes.ArraySplitByLength<int>(null!, 1).Count);
        TestAssert.Equal(0, ProtocolBytes.ArraySplitByLength(Array.Empty<int>(), 1).Count);
        TestAssert.Equal(0, ProtocolBytes.SplitIntegerToArray(0, 1).Length);
        TestAssert.True(new[] { 4, 4, 2 }.SequenceEqual(ProtocolBytes.SplitIntegerToArray(10, 4)));
        TestAssert.True(new[] { int.MaxValue }.SequenceEqual(ProtocolBytes.SplitIntegerToArray(int.MaxValue, int.MaxValue)));
        var read = AddressParameters.SplitReadLength(100, 10, 4);
        TestAssert.True(read.IsSuccess);
        TestAssert.True(new[] { 100, 104, 108 }.SequenceEqual(read.Content1));
        TestAssert.True(new[] { 4, 4, 2 }.SequenceEqual(read.Content2));
        var write = AddressParameters.SplitWriteData(100, source, 2, 2);
        TestAssert.True(write.IsSuccess);
        TestAssert.True(new[] { 100, 102 }.SequenceEqual(write.Content1));
        TestAssert.Bytes([1, 2, 3, 4], write.Content2[0]);
        TestAssert.Bytes([5, 6], write.Content2[1]);
        return Task.CompletedTask;
    }

    private static async Task OutOfRangeAsync(Action action, string parameter)
    {
        var error = await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
        {
            action();
            return Task.CompletedTask;
        });
        TestAssert.Equal(parameter, error.ParamName);
    }
}
