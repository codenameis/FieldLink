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
using static FieldLink.PlcDrivers.Panasonic.PanasonicMewtocolServerDefinitions;

namespace FieldLink.PlcDrivers.Panasonic
{
    /// <summary>PanasonicMewtocolServer 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class PanasonicMewtocolServerValueConverter
    {
        /// <summary>CreateFailedResponse 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string CreateFailedResponse(byte code) => "!" + code.ToString("D2");
    }
}
