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
using static FieldLink.PlcDrivers.Melsec.MelsecAddressParser;
using static FieldLink.PlcDrivers.Melsec.MelsecResponseParser;
using static FieldLink.PlcDrivers.Melsec.MelsecValueConverter;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>Melsec 요청 프레임을 생성합니다.</summary>
    public static class MelsecCommandBuilder
    {
        /// <summary>입력값으로 프로토콜 명령을 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "type">type에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildBytesFromAddress(int address, MelsecMcDataType type)
        {
            return Encoding.ASCII.GetBytes(address.ToString(type.FromBase == 10 ? "D6" : "X6"));
        }
    }
}
