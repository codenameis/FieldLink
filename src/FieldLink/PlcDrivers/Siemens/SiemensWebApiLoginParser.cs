using FieldLink.PlcDrivers.Common;
using Newtonsoft.Json.Linq;
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

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensWebApiLoginParser 프로토콜 값입니다.</summary>
    public static class SiemensWebApiLoginParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> ParseToken(string response)
        {
            try
            {
                JArray array = JArray.Parse(response);
                JObject json = (JObject)array[0];
                OperationResult check = SiemensWebApiResponseParser.CheckErrorResult(json);
                if (!check.IsSuccess)
                    return OperationResult.CreateFailedResult<string>(check);
                if (json.ContainsKey("result"))
                    return OperationResult.CreateSuccessResult(((JObject)json["result"]).Value<string>("token"));
                return new OperationResult<string>("로그인 응답에 result 또는 token이 없습니다: " + response);
            }
            catch (Exception ex)
            {
                return new OperationResult<string>("로그인 응답 해석 실패: " + ex.Message + Environment.NewLine + response);
            }
        }
    }
}
