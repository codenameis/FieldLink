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
using static FieldLink.PlcDrivers.Melsec.MelsecA1ENetCommandBuilder;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>MelsecA1ENet 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MelsecA1ENetResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckResponseLegal(byte[] response)
        {
            if (response.Length < 2)
                return new OperationResult(ProtocolMessages.ReceiveDataLengthTooShort);
            if (response[1] == 0)
                return OperationResult.CreateSuccessResult();
            if (response[1] == 0x5B)
                return new OperationResult(response[2], ProtocolMessages.MelsecPleaseReferToManualDocument);
            return new OperationResult(response[1], ProtocolMessages.MelsecPleaseReferToManualDocument);
        }

        /// <summary>프로토콜 응답에서 데이터와 상태 정보를 추출합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> ExtractActualData(byte[] response, bool isBit)
        {
            if (isBit)
            {
                // 비트 읽기
                byte[] Content = new byte[(response.Length - 2) * 2];
                for (int i = 2; i < response.Length; i++)
                {
                    if ((response[i] & 0x10) == 0x10)
                        Content[(i - 2) * 2 + 0] = 0x01;
                    if ((response[i] & 0x01) == 0x01)
                        Content[(i - 2) * 2 + 1] = 0x01;
                }

                return OperationResult.CreateSuccessResult(Content);
            }
            else
                return OperationResult.CreateSuccessResult(response.RemoveBegin(2));
        }
    }
}
