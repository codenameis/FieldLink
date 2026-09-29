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
using static FieldLink.PlcDrivers.Fuji.FujiSPBValueConverter;
using static FieldLink.PlcDrivers.Fuji.FujiSPBCommandBuilder;
using static FieldLink.PlcDrivers.Fuji.FujiSPBResponseParser;

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiSPB 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class FujiSpbAddressParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string AnalysisIntegerAddress(int address)
        {
            string tmp = address.ToString("D4");
            return tmp.Substring(2) + tmp.Substring(0, 2);
        }
    }
}
