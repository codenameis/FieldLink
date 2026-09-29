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
using static FieldLink.PlcDrivers.Vigor.VigorVsCommandBuilder;
using static FieldLink.PlcDrivers.Vigor.VigorVsResponseParser;

namespace FieldLink.PlcDrivers.Vigor
{
    /// <summary>VigorVs 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class VigorVsAddressParser
    {
        /// <summary>GetBytesAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static byte[] GetBytesAddress(int address)
        {
            string addressStart = address.ToString("D6");
            if (addressStart.Length > 6)
                addressStart = addressStart.Substring(6);
            return addressStart.ToHexBytes();
        }
    }
}
