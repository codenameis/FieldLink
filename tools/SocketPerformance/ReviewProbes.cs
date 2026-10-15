using System;
using FieldLink.PlcDrivers.Common;

// Regression probes for the review findings; exit zero only when both fixes are present.
internal static class ReviewProbes
{
    internal static int Run()
    {
        bool finalStageCalled = false;
        Func<int, OperationResult<int>> success = value => OperationResult.CreateSuccessResult(value + 1);
        Func<int, OperationResult<int>> rejected = value => new OperationResult<int>(913, "stage 9 rejected");
        Func<int, OperationResult<int>> final = value =>
        {
            finalStageCalled = true;
            return OperationResult.CreateSuccessResult(123);
        };
        var result = ProtocolConversions.GetResultFromOther(
            OperationResult.CreateSuccessResult(1),
            success, success, success, success, success, success, success, success, rejected, final);
        bool chainDefect = finalStageCalled && result.IsSuccess;
        Console.WriteLine("TenStageFailure: expected success=False, code=913, finalStageCalled=False; actual success=" +
            result.IsSuccess + ", code=" + result.ErrorCode + ", finalStageCalled=" + finalStageCalled +
            "; defectReproduced=" + chainDefect);

        Exception original = new InvalidOperationException("translator failure");
        var converted = ProtocolConversions.GetSuccessResultFromOther<int, int>(
            OperationResult.CreateSuccessResult(1), value => { throw original; });
        bool lostCause = !converted.IsSuccess && converted.FailureDetails == null;
        Console.WriteLine("ConversionCause: expected original exception retained or propagated; actual failureDetails=" +
            (converted.FailureDetails == null ? "null" : "present") + "; causeLost=" + lostCause);
        return !result.IsSuccess && result.ErrorCode == 913 && !finalStageCalled &&
            !converted.IsSuccess && ReferenceEquals(original, converted.FailureDetails?.Cause) ? 0 : 1;
    }
}
