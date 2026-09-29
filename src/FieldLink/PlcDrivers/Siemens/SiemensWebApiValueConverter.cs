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
using static FieldLink.PlcDrivers.Siemens.SiemensWebApiCommandBuilder;
using static FieldLink.PlcDrivers.Siemens.SiemensWebApiResponseParser;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensWebApi 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class SiemensWebApiValueConverter
    {
        /// <summary>GetJsonRpc 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "method">method에 사용할 입력값입니다.</param>
        /// <param name = "paramsJson">paramsJson에 사용할 입력값입니다.</param>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static JObject GetJsonRpc(string method, JObject paramsJson, long id)
        {
            JObject json = new JObject();
            json.Add("jsonrpc", new JValue("2.0"));
            json.Add("method", new JValue(method));
            json.Add("id", new JValue(id));
            if (paramsJson != null)
                json.Add("params", paramsJson);
            return json;
        }
    }
}
