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
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoCommandBuilder;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoAddressParser;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoResponseParser;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoControlCommandBuilder;

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>KeyenceNanoControl 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class KeyenceNanoControlResponseParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<KeyencePLCS> ParseReadPlcType(byte[] response)
        {
            var check = CheckPlcReadResponse(response);
            if (!check.IsSuccess)
                return check.ConvertFailed<KeyencePLCS>();
            string type = Encoding.ASCII.GetString(response.RemoveLast(2));
            switch (type)
            {
                case "48":
                case "49":
                    return OperationResult.CreateSuccessResult(KeyencePLCS.KV700);
                case "50":
                    return OperationResult.CreateSuccessResult(KeyencePLCS.KV1000);
                case "51":
                    return OperationResult.CreateSuccessResult(KeyencePLCS.KV3000);
                case "52":
                    return OperationResult.CreateSuccessResult(KeyencePLCS.KV5000);
                case "53":
                    return OperationResult.CreateSuccessResult(KeyencePLCS.KV5500);
                default:
                    return new OperationResult<KeyencePLCS>($"Unknow type:" + type);
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<int> ParseReadPlcMode(byte[] response)
        {
            var check = CheckPlcReadResponse(response);
            if (!check.IsSuccess)
                return check.ConvertFailed<int>();
            string type = Encoding.ASCII.GetString(response.RemoveLast(2));
            if (type == "0")
                return OperationResult.CreateSuccessResult(0);
            return OperationResult.CreateSuccessResult(1);
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "dateTime">dateTime에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult ParseSetPlcDateTime(byte[] response, DateTime dateTime)
        {
            return CheckPlcWriteResponse(response);
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> ParseReadAddressAnnotation(byte[] response, string address)
        {
            var check = CheckPlcReadResponse(response);
            if (!check.IsSuccess)
                return check.ConvertFailed<string>();
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetString(response.RemoveLast(2)).Trim(' '));
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "unit">unit에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ParseReadExpansionMemory(byte[] response, byte unit, ushort address, ushort length)
        {
            var check = CheckPlcReadResponse(response);
            if (!check.IsSuccess)
                return check.ConvertFailed<byte[]>();
            return ExtractActualData("DM", response); // DM에 따라 파싱하세요, 왜냐하면 위의 명령은 .U 형식이고, DM의 기본 명령은 .U 형식입니다.
        }
    }
}
