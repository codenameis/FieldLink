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
using static FieldLink.PlcDrivers.Melsec.MelsecA1EAsciiNetCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecA1EAsciiNet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MelsecA1EAsciiNetResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckResponseLegal(byte[] response)
        {
            if (response.Length < 4)
                return new OperationResult(ProtocolMessages.ReceiveDataLengthTooShort);
            if (response[2] == 0x30 && response[3] == 0x30)
                return OperationResult.CreateSuccessResult();
            if (response[2] == 0x35 && response[3] == 0x42)
                return new OperationResult(Convert.ToInt32(Encoding.ASCII.GetString(response, 4, 2), 16), ProtocolMessages.MelsecPleaseReferToManualDocument);
            return new OperationResult(Convert.ToInt32(Encoding.ASCII.GetString(response, 2, 2), 16), ProtocolMessages.MelsecPleaseReferToManualDocument);
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtractActualData(byte[] response, bool isBit)
        {
            if (isBit)
                return OperationResult.CreateSuccessResult(response.RemoveBegin(4).Select(m => m == 0x30 ? (byte)0x00 : (byte)0x01).ToArray());
            else
                return OperationResult.CreateSuccessResult(MelsecValueConverter.TransAsciiByteArrayToByteArray(response.RemoveBegin(4)));
        }
    }
}
