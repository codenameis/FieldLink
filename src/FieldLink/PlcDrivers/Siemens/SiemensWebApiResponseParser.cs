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
using static FieldLink.PlcDrivers.Siemens.SiemensWebApiValueConverter;
using static FieldLink.PlcDrivers.Siemens.SiemensWebApiCommandBuilder;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensWebApi 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class SiemensWebApiResponseParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "json">json에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckErrorResult(JObject json)
        {
            if (json.ContainsKey("error"))
            {
                JObject error = json["error"] as JObject;
                int code = error.Value<int>("code");
                string message = error.Value<string>("message");
                return new OperationResult(code, message);
            }

            return OperationResult.CreateSuccessResult();
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> CheckReadRawResult(string response)
        {
            OperationResult<JToken> checkJson = CheckAndExtraOneJsonResult(response);
            if (!checkJson.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(checkJson);
            JArray content = checkJson.Content as JArray;
            return OperationResult.CreateSuccessResult(content.Select(m => m.Value<byte>()).ToArray());
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult CheckWriteResult(string response)
        {
            JArray resultArray = JArray.Parse(response);
            JObject json = (JObject)resultArray[0];
            OperationResult checkResult = CheckErrorResult(json);
            if (!checkResult.IsSuccess)
                return OperationResult.CreateFailedResult<JToken>(checkResult);
            if (json.ContainsKey("result"))
            {
                bool value = json["result"].Value<bool>();
                return value ? OperationResult.CreateSuccessResult() : new OperationResult(json.ToString());
            }

            return new OperationResult<JToken>("Can't find result key and none token, login failed:" + Environment.NewLine + response);
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<JToken> CheckAndExtraOneJsonResult(string response)
        {
            OperationResult<JToken[]> extra = CheckAndExtraJsonResult(response);
            if (!extra.IsSuccess)
                return OperationResult.CreateFailedResult<JToken>(extra);
            return OperationResult.CreateSuccessResult(extra.Content[0]);
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<JToken[]> CheckAndExtraJsonResult(string response)
        {
            try
            {
                JArray jArray = JArray.Parse(response);
                List<JToken> results = new List<JToken>();
                for (int i = 0; i < jArray.Count; i++)
                {
                    JObject json = jArray[i] as JObject;
                    if (json == null)
                        continue;
                    OperationResult checkResult = CheckErrorResult(json);
                    if (!checkResult.IsSuccess)
                        return OperationResult.CreateFailedResult<JToken[]>(checkResult);
                    if (json.ContainsKey("result"))
                        results.Add(json["result"]);
                    else
                        return new OperationResult<JToken[]>("Can't find result key and none token, login failed:" + Environment.NewLine + response);
                }

                return OperationResult.CreateSuccessResult(results.ToArray());
            }
            catch (Exception ex)
            {
                return new OperationResult<JToken[]>("CheckAndExtraJsonResult failed: " + ex.Message + Environment.NewLine + "Content: " + response);
            }
        }
    }
}
