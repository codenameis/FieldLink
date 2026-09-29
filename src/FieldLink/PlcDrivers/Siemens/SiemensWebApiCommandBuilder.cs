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
using static FieldLink.PlcDrivers.Siemens.SiemensWebApiResponseParser;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensWebApi 요청 프레임을 생성합니다.</summary>
    public static class SiemensWebApiCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "method">method에 사용할 입력값입니다.</param>
        /// <param name = "paramsJson">paramsJson에 사용할 입력값입니다.</param>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static JArray BuildRequestBody(string method, JObject paramsJson, long id)
        {
            return new JArray()
            {
                GetJsonRpc(method, paramsJson, id)
            };
        }

        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "name">name에 사용할 입력값입니다.</param>
        /// <param name = "password">password에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static JArray BuildConnectBody(long id, string name, string password)
        {
            JObject paras = new JObject();
            paras.Add("user", new JValue(name));
            paras.Add("password", new JValue(password));
            return BuildRequestBody("Api.Login", paras, id);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static JArray BuildReadRawBody(long id, string address)
        {
            JObject paras = new JObject();
            paras.Add("var", new JValue(address));
            paras.Add("mode", new JValue("raw"));
            return BuildRequestBody("PlcProgram.Read", paras, id);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static JArray BuildWriteRawBody(long id, string address, byte[] value)
        {
            JObject paras = new JObject();
            paras.Add("var", new JValue(address));
            paras.Add("mode", new JValue("raw"));
            paras.Add("value", new JArray(value.Select(m => (int)m).ToArray()));
            return BuildRequestBody("PlcProgram.Write", paras, id);
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static JArray BuildWriteJTokenBody(long id, string address, JToken value)
        {
            JObject paras = new JObject();
            paras.Add("var", new JValue(address));
            paras.Add("value", value);
            return BuildRequestBody("PlcProgram.Write", paras, id);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static JArray BuildReadJTokenBody(long id, string address)
        {
            JObject paras = new JObject();
            paras.Add("var", new JValue(address));
            return BuildRequestBody("PlcProgram.Read", paras, id);
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "id">id에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static JArray BuildReadJTokenBody(long id, string[] address)
        {
            JArray jArray = new JArray();
            for (int i = 0; i < address.Length; i++)
            {
                JObject paras = new JObject();
                paras.Add("var", new JValue(address[i]));
                jArray.Add(GetJsonRpc("PlcProgram.Read", paras, id + i));
            }

            return jArray;
        }
    }
}
