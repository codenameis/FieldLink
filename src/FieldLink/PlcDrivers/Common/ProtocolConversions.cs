using FieldLink.PlcDrivers.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>ProtocolConversions 프로토콜 값입니다.</summary>
    public static class ProtocolConversions
    {
        /// <summary>GetResultFromBytes 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "translator">translator에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromBytes<TResult>(OperationResult<byte[]> result, Func<byte[], TResult> translator)
        {
            try
            {
                if (result.IsSuccess)
                    return OperationResult.CreateSuccessResult(translator(result.Content));
                else
                    return OperationResult.CreateFailedResult<TResult>(result);
            }
            catch (Exception ex)
            {
                return new OperationResult<TResult>()
                {
                    Message = $"{ProtocolMessages.DataTransformError} {ProtocolBytes.ByteToHexString(result.Content)} : Length({result.Content.Length}) {ex.Message}"};
            }
        }

        /// <summary>GetResultFromArray 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromArray<TResult>(OperationResult<TResult[]> result) => GetSuccessResultFromOther(result, m => m[0]);
        /// <summary>GetSuccessResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans">trans에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetSuccessResultFromOther<TResult, TIn>(OperationResult<TIn> result, Func<TIn, TResult> trans)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            try
            {
                TResult value = trans(result.Content);
                return OperationResult.CreateSuccessResult(value);
            }
            catch (Exception ex)
            {
                return new OperationResult<TResult>($"{ProtocolMessages.DataTransformError} {ex.Message}");
            }
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans">trans에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TIn">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult GetResultFromOther<TIn>(OperationResult<TIn> result, Func<TIn, OperationResult> trans)
        {
            if (!result.IsSuccess)
                return result;
            return trans(result.Content);
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans">trans에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn>(OperationResult<TIn> result, Func<TIn, OperationResult<TResult>> trans)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            return trans(result.Content);
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans1">trans1에 사용할 입력값입니다.</param>
        /// <param name = "trans2">trans2에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2>(OperationResult<TIn1> result, Func<TIn1, OperationResult<TIn2>> trans1, Func<TIn2, OperationResult<TResult>> trans2)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> result1 = trans1(result.Content);
            if (!result1.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result1);
            return trans2(result1.Content);
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans1">trans1에 사용할 입력값입니다.</param>
        /// <param name = "trans2">trans2에 사용할 입력값입니다.</param>
        /// <param name = "trans3">trans3에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3>(OperationResult<TIn1> result, Func<TIn1, OperationResult<TIn2>> trans1, Func<TIn2, OperationResult<TIn3>> trans2, Func<TIn3, OperationResult<TResult>> trans3)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> result1 = trans1(result.Content);
            if (!result1.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result1);
            OperationResult<TIn3> result2 = trans2(result1.Content);
            if (!result2.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result2);
            return trans3(result2.Content);
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans1">trans1에 사용할 입력값입니다.</param>
        /// <param name = "trans2">trans2에 사용할 입력값입니다.</param>
        /// <param name = "trans3">trans3에 사용할 입력값입니다.</param>
        /// <param name = "trans4">trans4에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4>(OperationResult<TIn1> result, Func<TIn1, OperationResult<TIn2>> trans1, Func<TIn2, OperationResult<TIn3>> trans2, Func<TIn3, OperationResult<TIn4>> trans3, Func<TIn4, OperationResult<TResult>> trans4)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> result1 = trans1(result.Content);
            if (!result1.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result1);
            OperationResult<TIn3> result2 = trans2(result1.Content);
            if (!result2.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result2);
            OperationResult<TIn4> result3 = trans3(result2.Content);
            if (!result3.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result3);
            return trans4(result3.Content);
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans1">trans1에 사용할 입력값입니다.</param>
        /// <param name = "trans2">trans2에 사용할 입력값입니다.</param>
        /// <param name = "trans3">trans3에 사용할 입력값입니다.</param>
        /// <param name = "trans4">trans4에 사용할 입력값입니다.</param>
        /// <param name = "trans5">trans5에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn5">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5>(OperationResult<TIn1> result, Func<TIn1, OperationResult<TIn2>> trans1, Func<TIn2, OperationResult<TIn3>> trans2, Func<TIn3, OperationResult<TIn4>> trans3, Func<TIn4, OperationResult<TIn5>> trans4, Func<TIn5, OperationResult<TResult>> trans5)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> result1 = trans1(result.Content);
            if (!result1.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result1);
            OperationResult<TIn3> result2 = trans2(result1.Content);
            if (!result2.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result2);
            OperationResult<TIn4> result3 = trans3(result2.Content);
            if (!result3.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result3);
            OperationResult<TIn5> result4 = trans4(result3.Content);
            if (!result4.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result4);
            return trans5(result4.Content);
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans1">trans1에 사용할 입력값입니다.</param>
        /// <param name = "trans2">trans2에 사용할 입력값입니다.</param>
        /// <param name = "trans3">trans3에 사용할 입력값입니다.</param>
        /// <param name = "trans4">trans4에 사용할 입력값입니다.</param>
        /// <param name = "trans5">trans5에 사용할 입력값입니다.</param>
        /// <param name = "trans6">trans6에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn5">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn6">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5, TIn6>(OperationResult<TIn1> result, Func<TIn1, OperationResult<TIn2>> trans1, Func<TIn2, OperationResult<TIn3>> trans2, Func<TIn3, OperationResult<TIn4>> trans3, Func<TIn4, OperationResult<TIn5>> trans4, Func<TIn5, OperationResult<TIn6>> trans5, Func<TIn6, OperationResult<TResult>> trans6)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> result1 = trans1(result.Content);
            if (!result1.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result1);
            OperationResult<TIn3> result2 = trans2(result1.Content);
            if (!result2.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result2);
            OperationResult<TIn4> result3 = trans3(result2.Content);
            if (!result3.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result3);
            OperationResult<TIn5> result4 = trans4(result3.Content);
            if (!result4.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result4);
            OperationResult<TIn6> result5 = trans5(result4.Content);
            if (!result5.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result5);
            return trans6(result5.Content);
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans1">trans1에 사용할 입력값입니다.</param>
        /// <param name = "trans2">trans2에 사용할 입력값입니다.</param>
        /// <param name = "trans3">trans3에 사용할 입력값입니다.</param>
        /// <param name = "trans4">trans4에 사용할 입력값입니다.</param>
        /// <param name = "trans5">trans5에 사용할 입력값입니다.</param>
        /// <param name = "trans6">trans6에 사용할 입력값입니다.</param>
        /// <param name = "trans7">trans7에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        /// <typeparam name = "TResult">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn1">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn2">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn3">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn4">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn5">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn6">결과 또는 변환 데이터의 형식입니다.</typeparam>
        /// <typeparam name = "TIn7">결과 또는 변환 데이터의 형식입니다.</typeparam>
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7>(OperationResult<TIn1> result, Func<TIn1, OperationResult<TIn2>> trans1, Func<TIn2, OperationResult<TIn3>> trans2, Func<TIn3, OperationResult<TIn4>> trans3, Func<TIn4, OperationResult<TIn5>> trans4, Func<TIn5, OperationResult<TIn6>> trans5, Func<TIn6, OperationResult<TIn7>> trans6, Func<TIn7, OperationResult<TResult>> trans7)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> result1 = trans1(result.Content);
            if (!result1.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result1);
            OperationResult<TIn3> result2 = trans2(result1.Content);
            if (!result2.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result2);
            OperationResult<TIn4> result3 = trans3(result2.Content);
            if (!result3.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result3);
            OperationResult<TIn5> result4 = trans4(result3.Content);
            if (!result4.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result4);
            OperationResult<TIn6> result5 = trans5(result4.Content);
            if (!result5.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result5);
            OperationResult<TIn7> result6 = trans6(result5.Content);
            if (!result6.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result6);
            return trans7(result6.Content);
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans1">trans1에 사용할 입력값입니다.</param>
        /// <param name = "trans2">trans2에 사용할 입력값입니다.</param>
        /// <param name = "trans3">trans3에 사용할 입력값입니다.</param>
        /// <param name = "trans4">trans4에 사용할 입력값입니다.</param>
        /// <param name = "trans5">trans5에 사용할 입력값입니다.</param>
        /// <param name = "trans6">trans6에 사용할 입력값입니다.</param>
        /// <param name = "trans7">trans7에 사용할 입력값입니다.</param>
        /// <param name = "trans8">trans8에 사용할 입력값입니다.</param>
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
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7, TIn8>(OperationResult<TIn1> result, Func<TIn1, OperationResult<TIn2>> trans1, Func<TIn2, OperationResult<TIn3>> trans2, Func<TIn3, OperationResult<TIn4>> trans3, Func<TIn4, OperationResult<TIn5>> trans4, Func<TIn5, OperationResult<TIn6>> trans5, Func<TIn6, OperationResult<TIn7>> trans6, Func<TIn7, OperationResult<TIn8>> trans7, Func<TIn8, OperationResult<TResult>> trans8)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> result1 = trans1(result.Content);
            if (!result1.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result1);
            OperationResult<TIn3> result2 = trans2(result1.Content);
            if (!result2.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result2);
            OperationResult<TIn4> result3 = trans3(result2.Content);
            if (!result3.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result3);
            OperationResult<TIn5> result4 = trans4(result3.Content);
            if (!result4.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result4);
            OperationResult<TIn6> result5 = trans5(result4.Content);
            if (!result5.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result5);
            OperationResult<TIn7> result6 = trans6(result5.Content);
            if (!result6.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result6);
            OperationResult<TIn8> result7 = trans7(result6.Content);
            if (!result7.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result7);
            return trans8(result7.Content);
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans1">trans1에 사용할 입력값입니다.</param>
        /// <param name = "trans2">trans2에 사용할 입력값입니다.</param>
        /// <param name = "trans3">trans3에 사용할 입력값입니다.</param>
        /// <param name = "trans4">trans4에 사용할 입력값입니다.</param>
        /// <param name = "trans5">trans5에 사용할 입력값입니다.</param>
        /// <param name = "trans6">trans6에 사용할 입력값입니다.</param>
        /// <param name = "trans7">trans7에 사용할 입력값입니다.</param>
        /// <param name = "trans8">trans8에 사용할 입력값입니다.</param>
        /// <param name = "trans9">trans9에 사용할 입력값입니다.</param>
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
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7, TIn8, TIn9>(OperationResult<TIn1> result, Func<TIn1, OperationResult<TIn2>> trans1, Func<TIn2, OperationResult<TIn3>> trans2, Func<TIn3, OperationResult<TIn4>> trans3, Func<TIn4, OperationResult<TIn5>> trans4, Func<TIn5, OperationResult<TIn6>> trans5, Func<TIn6, OperationResult<TIn7>> trans6, Func<TIn7, OperationResult<TIn8>> trans7, Func<TIn8, OperationResult<TIn9>> trans8, Func<TIn9, OperationResult<TResult>> trans9)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> result1 = trans1(result.Content);
            if (!result1.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result1);
            OperationResult<TIn3> result2 = trans2(result1.Content);
            if (!result2.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result2);
            OperationResult<TIn4> result3 = trans3(result2.Content);
            if (!result3.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result3);
            OperationResult<TIn5> result4 = trans4(result3.Content);
            if (!result4.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result4);
            OperationResult<TIn6> result5 = trans5(result4.Content);
            if (!result5.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result5);
            OperationResult<TIn7> result6 = trans6(result5.Content);
            if (!result6.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result6);
            OperationResult<TIn8> result7 = trans7(result6.Content);
            if (!result7.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result7);
            OperationResult<TIn9> result8 = trans8(result7.Content);
            if (!result8.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result8);
            return trans9(result8.Content);
        }

        /// <summary>GetResultFromOther 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "result">result에 사용할 입력값입니다.</param>
        /// <param name = "trans1">trans1에 사용할 입력값입니다.</param>
        /// <param name = "trans2">trans2에 사용할 입력값입니다.</param>
        /// <param name = "trans3">trans3에 사용할 입력값입니다.</param>
        /// <param name = "trans4">trans4에 사용할 입력값입니다.</param>
        /// <param name = "trans5">trans5에 사용할 입력값입니다.</param>
        /// <param name = "trans6">trans6에 사용할 입력값입니다.</param>
        /// <param name = "trans7">trans7에 사용할 입력값입니다.</param>
        /// <param name = "trans8">trans8에 사용할 입력값입니다.</param>
        /// <param name = "trans9">trans9에 사용할 입력값입니다.</param>
        /// <param name = "trans10">trans10에 사용할 입력값입니다.</param>
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
        public static OperationResult<TResult> GetResultFromOther<TResult, TIn1, TIn2, TIn3, TIn4, TIn5, TIn6, TIn7, TIn8, TIn9, TIn10>(OperationResult<TIn1> result, Func<TIn1, OperationResult<TIn2>> trans1, Func<TIn2, OperationResult<TIn3>> trans2, Func<TIn3, OperationResult<TIn4>> trans3, Func<TIn4, OperationResult<TIn5>> trans4, Func<TIn5, OperationResult<TIn6>> trans5, Func<TIn6, OperationResult<TIn7>> trans6, Func<TIn7, OperationResult<TIn8>> trans7, Func<TIn8, OperationResult<TIn9>> trans8, Func<TIn9, OperationResult<TIn10>> trans9, Func<TIn10, OperationResult<TResult>> trans10)
        {
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result);
            OperationResult<TIn2> result1 = trans1(result.Content);
            if (!result1.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result1);
            OperationResult<TIn3> result2 = trans2(result1.Content);
            if (!result2.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result2);
            OperationResult<TIn4> result3 = trans3(result2.Content);
            if (!result3.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result3);
            OperationResult<TIn5> result4 = trans4(result3.Content);
            if (!result4.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result4);
            OperationResult<TIn6> result5 = trans5(result4.Content);
            if (!result5.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result5);
            OperationResult<TIn7> result6 = trans6(result5.Content);
            if (!result6.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result6);
            OperationResult<TIn8> result7 = trans7(result6.Content);
            if (!result7.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result7);
            OperationResult<TIn9> result8 = trans8(result7.Content);
            if (!result8.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result8);
            OperationResult<TIn10> result9 = trans9(result8.Content);
            if (!result.IsSuccess)
                return OperationResult.CreateFailedResult<TResult>(result9);
            return trans10(result9.Content);
        }
    }
}
