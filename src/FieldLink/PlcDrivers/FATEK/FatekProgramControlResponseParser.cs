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
using static FieldLink.PlcDrivers.FATEK.FatekProgramValueConverter;
using static FieldLink.PlcDrivers.FATEK.FatekProgramResponseParser;
using static FieldLink.PlcDrivers.FATEK.FatekProgramCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekControlCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekProgramControlCommandBuilder;

namespace FieldLink.PlcDrivers.FATEK
{
    /// <summary>FatekProgramControl 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class FatekProgramControlResponseParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<bool[]> ParseReadStatus(byte[] response, byte station)
        {
            OperationResult check = CheckResponse(response);
            if (!check.IsSuccess)
                return check.ConvertFailed<bool[]>();
            return OperationResult.CreateSuccessResult(Encoding.ASCII.GetString(response, 6, 2).ToHexBytes().ToBoolArray());
        }
    }
}
