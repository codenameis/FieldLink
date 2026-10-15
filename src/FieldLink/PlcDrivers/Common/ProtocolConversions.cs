using System;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>작업 결과를 다른 값으로 변환하고, 중간에 발생한 실패를 다음 호출자에게 전달합니다.</summary>
    public static class ProtocolConversions
    {
        /// <summary>성공한 바이트 응답을 변환합니다. 기존 실패는 전달하고 변환 예외는 FailureDetails.Cause에 보존합니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "translator">성공한 바이트 배열을 원하는 값으로 변환하는 함수입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromBytes<TResult>(OperationResult<byte[]> result, Func<byte[], TResult> translator)
        {
            try
            {
                if (!result.IsSuccess)
                    return OperationResult.CreateFailedResult<TResult>(result);

                TResult convertedValue = translator(result.Content);
                return OperationResult.CreateSuccessResult(convertedValue);
            }
            catch (Exception conversionError)
            {
                return new OperationResult<TResult>()
                {
                    Message = $"{ProtocolMessages.DataTransformError} {ProtocolBytes.ByteToHexString(result.Content)} : Length({result.Content?.Length ?? 0}) {conversionError.Message}",
                    FailureDetails = new OperationFailureDetails(null, "Convert", null, 0, 0, conversionError)
                };
            }
        }

        /// <summary>성공한 배열 결과에서 첫 번째 원소를 꺼냅니다. 빈 배열 등의 변환 실패는 실패 결과로 반환합니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromArray<TResult>(OperationResult<TResult[]> result)
        {
            return GetSuccessResultFromOther(result, values => values[0]);
        }

        /// <summary>성공한 결과의 값을 변환합니다. 기존 실패는 전달하고 변환 예외는 FailureDetails.Cause에 보존합니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans">성공한 입력값을 다음 결과로 변환하는 함수입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetSuccessResultFromOther<TResult, TIn>(OperationResult<TIn> result, Func<TIn, TResult> trans)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);

            try
            {
                TResult convertedValue = trans(result.Content);
                return OperationResult.CreateSuccessResult(convertedValue);
            }
            catch (Exception conversionError)
            {
                return new OperationResult<TResult>($"{ProtocolMessages.DataTransformError} {conversionError.Message}")
                {
                    FailureDetails = new OperationFailureDetails(null, "Convert", null, 0, 0, conversionError)
                };
            }
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans">성공한 입력값을 다음 결과로 변환하는 함수입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TIn">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult GetResultFromOther<TIn>(OperationResult<TIn> result, Func<TIn, OperationResult> trans)
        {
            if (!result.IsSuccess)
                return result;
            return trans(result.Content);
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans">성공한 입력값을 다음 결과로 변환하는 함수입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn>(OperationResult<TIn> result, Func<TIn, OperationResult<TResult>> trans)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            return trans(result.Content);
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans1">1단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans2">2단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2>(
            OperationResult<TIn1> result,
            Func<TIn1, OperationResult<TIn2>> trans1,
            Func<TIn2, OperationResult<TResult>> trans2)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> stage1Result = trans1(result.Content);
            if (!stage1Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage1Result);
            return trans2(stage1Result.Content);
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans1">1단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans2">2단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans3">3단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3>(
            OperationResult<TIn1> result,
            Func<TIn1, OperationResult<TIn2>> trans1,
            Func<TIn2, OperationResult<TIn3>> trans2,
            Func<TIn3, OperationResult<TResult>> trans3)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> stage1Result = trans1(result.Content);
            if (!stage1Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage1Result);
            OperationResult<TIn3> stage2Result = trans2(stage1Result.Content);
            if (!stage2Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage2Result);
            return trans3(stage2Result.Content);
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans1">1단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans2">2단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans3">3단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans4">4단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4>(
            OperationResult<TIn1> result,
            Func<TIn1, OperationResult<TIn2>> trans1,
            Func<TIn2, OperationResult<TIn3>> trans2,
            Func<TIn3, OperationResult<TIn4>> trans3,
            Func<TIn4, OperationResult<TResult>> trans4)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> stage1Result = trans1(result.Content);
            if (!stage1Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage1Result);
            OperationResult<TIn3> stage2Result = trans2(stage1Result.Content);
            if (!stage2Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage2Result);
            OperationResult<TIn4> stage3Result = trans3(stage2Result.Content);
            if (!stage3Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage3Result);
            return trans4(stage3Result.Content);
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans1">1단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans2">2단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans3">3단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans4">4단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans5">5단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn5">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5>(
            OperationResult<TIn1> result,
            Func<TIn1, OperationResult<TIn2>> trans1,
            Func<TIn2, OperationResult<TIn3>> trans2,
            Func<TIn3, OperationResult<TIn4>> trans3,
            Func<TIn4, OperationResult<TIn5>> trans4,
            Func<TIn5, OperationResult<TResult>> trans5)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> stage1Result = trans1(result.Content);
            if (!stage1Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage1Result);
            OperationResult<TIn3> stage2Result = trans2(stage1Result.Content);
            if (!stage2Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage2Result);
            OperationResult<TIn4> stage3Result = trans3(stage2Result.Content);
            if (!stage3Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage3Result);
            OperationResult<TIn5> stage4Result = trans4(stage3Result.Content);
            if (!stage4Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage4Result);
            return trans5(stage4Result.Content);
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans1">1단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans2">2단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans3">3단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans4">4단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans5">5단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans6">6단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn5">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn6">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5, TIn6>(
            OperationResult<TIn1> result,
            Func<TIn1, OperationResult<TIn2>> trans1,
            Func<TIn2, OperationResult<TIn3>> trans2,
            Func<TIn3, OperationResult<TIn4>> trans3,
            Func<TIn4, OperationResult<TIn5>> trans4,
            Func<TIn5, OperationResult<TIn6>> trans5,
            Func<TIn6, OperationResult<TResult>> trans6)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> stage1Result = trans1(result.Content);
            if (!stage1Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage1Result);
            OperationResult<TIn3> stage2Result = trans2(stage1Result.Content);
            if (!stage2Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage2Result);
            OperationResult<TIn4> stage3Result = trans3(stage2Result.Content);
            if (!stage3Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage3Result);
            OperationResult<TIn5> stage4Result = trans4(stage3Result.Content);
            if (!stage4Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage4Result);
            OperationResult<TIn6> stage5Result = trans5(stage4Result.Content);
            if (!stage5Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage5Result);
            return trans6(stage5Result.Content);
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans1">1단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans2">2단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans3">3단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans4">4단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans5">5단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans6">6단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans7">7단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn5">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn6">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn7">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7>(
            OperationResult<TIn1> result,
            Func<TIn1, OperationResult<TIn2>> trans1,
            Func<TIn2, OperationResult<TIn3>> trans2,
            Func<TIn3, OperationResult<TIn4>> trans3,
            Func<TIn4, OperationResult<TIn5>> trans4,
            Func<TIn5, OperationResult<TIn6>> trans5,
            Func<TIn6, OperationResult<TIn7>> trans6,
            Func<TIn7, OperationResult<TResult>> trans7)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> stage1Result = trans1(result.Content);
            if (!stage1Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage1Result);
            OperationResult<TIn3> stage2Result = trans2(stage1Result.Content);
            if (!stage2Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage2Result);
            OperationResult<TIn4> stage3Result = trans3(stage2Result.Content);
            if (!stage3Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage3Result);
            OperationResult<TIn5> stage4Result = trans4(stage3Result.Content);
            if (!stage4Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage4Result);
            OperationResult<TIn6> stage5Result = trans5(stage4Result.Content);
            if (!stage5Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage5Result);
            OperationResult<TIn7> stage6Result = trans6(stage5Result.Content);
            if (!stage6Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage6Result);
            return trans7(stage6Result.Content);
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans1">1단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans2">2단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans3">3단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans4">4단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans5">5단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans6">6단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans7">7단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans8">8단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn5">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn6">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn7">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn8">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7, TIn8>(
            OperationResult<TIn1> result,
            Func<TIn1, OperationResult<TIn2>> trans1,
            Func<TIn2, OperationResult<TIn3>> trans2,
            Func<TIn3, OperationResult<TIn4>> trans3,
            Func<TIn4, OperationResult<TIn5>> trans4,
            Func<TIn5, OperationResult<TIn6>> trans5,
            Func<TIn6, OperationResult<TIn7>> trans6,
            Func<TIn7, OperationResult<TIn8>> trans7,
            Func<TIn8, OperationResult<TResult>> trans8)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> stage1Result = trans1(result.Content);
            if (!stage1Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage1Result);
            OperationResult<TIn3> stage2Result = trans2(stage1Result.Content);
            if (!stage2Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage2Result);
            OperationResult<TIn4> stage3Result = trans3(stage2Result.Content);
            if (!stage3Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage3Result);
            OperationResult<TIn5> stage4Result = trans4(stage3Result.Content);
            if (!stage4Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage4Result);
            OperationResult<TIn6> stage5Result = trans5(stage4Result.Content);
            if (!stage5Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage5Result);
            OperationResult<TIn7> stage6Result = trans6(stage5Result.Content);
            if (!stage6Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage6Result);
            OperationResult<TIn8> stage7Result = trans7(stage6Result.Content);
            if (!stage7Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage7Result);
            return trans8(stage7Result.Content);
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans1">1단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans2">2단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans3">3단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans4">4단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans5">5단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans6">6단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans7">7단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans8">8단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans9">9단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn5">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn6">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn7">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn8">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn9">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7, TIn8, TIn9>(
            OperationResult<TIn1> result,
            Func<TIn1, OperationResult<TIn2>> trans1,
            Func<TIn2, OperationResult<TIn3>> trans2,
            Func<TIn3, OperationResult<TIn4>> trans3,
            Func<TIn4, OperationResult<TIn5>> trans4,
            Func<TIn5, OperationResult<TIn6>> trans5,
            Func<TIn6, OperationResult<TIn7>> trans6,
            Func<TIn7, OperationResult<TIn8>> trans7,
            Func<TIn8, OperationResult<TIn9>> trans8,
            Func<TIn9, OperationResult<TResult>> trans9)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> stage1Result = trans1(result.Content);
            if (!stage1Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage1Result);
            OperationResult<TIn3> stage2Result = trans2(stage1Result.Content);
            if (!stage2Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage2Result);
            OperationResult<TIn4> stage3Result = trans3(stage2Result.Content);
            if (!stage3Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage3Result);
            OperationResult<TIn5> stage4Result = trans4(stage3Result.Content);
            if (!stage4Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage4Result);
            OperationResult<TIn6> stage5Result = trans5(stage4Result.Content);
            if (!stage5Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage5Result);
            OperationResult<TIn7> stage6Result = trans6(stage5Result.Content);
            if (!stage6Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage6Result);
            OperationResult<TIn8> stage7Result = trans7(stage6Result.Content);
            if (!stage7Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage7Result);
            OperationResult<TIn9> stage8Result = trans8(stage7Result.Content);
            if (!stage8Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage8Result);
            return trans9(stage8Result.Content);
        }

        /// <summary>앞 단계가 성공한 경우에만 다음 변환을 실행합니다. 실패하면 이후 변환은 실행하지 않습니다.</summary>
        /// <param name = "result">변환할 값 또는 이전 단계의 실패를 담은 결과입니다.</param>
        /// <param name = "trans1">1단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans2">2단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans3">3단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans4">4단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans5">5단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans6">6단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans7">7단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans8">8단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans9">9단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <param name = "trans10">10단계의 변환 함수입니다. 앞 단계가 성공해야 실행됩니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn5">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn6">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn7">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn8">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn9">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn10">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7, TIn8, TIn9, TIn10>(
            OperationResult<TIn1> result,
            Func<TIn1, OperationResult<TIn2>> trans1,
            Func<TIn2, OperationResult<TIn3>> trans2,
            Func<TIn3, OperationResult<TIn4>> trans3,
            Func<TIn4, OperationResult<TIn5>> trans4,
            Func<TIn5, OperationResult<TIn6>> trans5,
            Func<TIn6, OperationResult<TIn7>> trans6,
            Func<TIn7, OperationResult<TIn8>> trans7,
            Func<TIn8, OperationResult<TIn9>> trans8,
            Func<TIn9, OperationResult<TIn10>> trans9,
            Func<TIn10, OperationResult<TResult>> trans10)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> stage1Result = trans1(result.Content);
            if (!stage1Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage1Result);
            OperationResult<TIn3> stage2Result = trans2(stage1Result.Content);
            if (!stage2Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage2Result);
            OperationResult<TIn4> stage3Result = trans3(stage2Result.Content);
            if (!stage3Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage3Result);
            OperationResult<TIn5> stage4Result = trans4(stage3Result.Content);
            if (!stage4Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage4Result);
            OperationResult<TIn6> stage5Result = trans5(stage4Result.Content);
            if (!stage5Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage5Result);
            OperationResult<TIn7> stage6Result = trans6(stage5Result.Content);
            if (!stage6Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage6Result);
            OperationResult<TIn8> stage7Result = trans7(stage6Result.Content);
            if (!stage7Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage7Result);
            OperationResult<TIn9> stage8Result = trans8(stage7Result.Content);
            if (!stage8Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage8Result);
            OperationResult<TIn10> stage9Result = trans9(stage8Result.Content);
            if (!stage9Result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(stage9Result);
            return trans10(stage9Result.Content);
        }
    }
}
