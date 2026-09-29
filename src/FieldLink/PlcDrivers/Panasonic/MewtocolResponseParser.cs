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

namespace FieldLink.PlcDrivers.Panasonic
{
    /// <summary>Mewtocol 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MewtocolResponseParser
    {
        /// <summary>GetPlcType 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> GetPlcType(byte[] data)
        {
            try
            {
                string code = Encoding.ASCII.GetString(data, 0, 2);
                switch (code)
                {
                    case "03":
                        return OperationResult.CreateSuccessResult("FP3");
                    case "02":
                        return OperationResult.CreateSuccessResult("FP5");
                    case "05":
                        return OperationResult.CreateSuccessResult("FP-E");
                    default:
                        return OperationResult.CreateSuccessResult(code);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<string>("Get plctype failed : " + ex.Message + Environment.NewLine + "Source: " + data.ToHexString());
            }
        }
    }
}
