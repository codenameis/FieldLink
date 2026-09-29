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

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>CipSession 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class CipSessionResponseParser
    {
        /// <summary>세션 등록 응답의 상태를 확인하고 Encapsulation 세션 식별자를 반환합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<uint> ParseRegisterSession(byte[] response)
        {
            OperationResult check = AllenBradleyResponseParser.CheckResponse(response);
            if (!check.IsSuccess)
                return OperationResult.CreateFailedResult<uint>(check);
            return OperationResult.CreateSuccessResult(BitConverter.ToUInt32(response, 4));
        }
    }
}
