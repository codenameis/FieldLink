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

namespace FieldLink.PlcDrivers.OpenProtocol
{
    /// <summary>OpenProtocolTimeParser 프로토콜 값입니다.</summary>
    public static class OpenProtocolTimeParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static DateTime ParseTime(string response) => DateTime.ParseExact(response.Substring(20, 19), "yyyy-MM-dd:HH:mm:ss", null);
    }
}
