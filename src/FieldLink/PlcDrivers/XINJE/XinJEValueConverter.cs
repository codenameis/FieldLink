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
using static FieldLink.PlcDrivers.XINJE.XinjeAddressParser;
using static FieldLink.PlcDrivers.XINJE.XinJECommandBuilder;

namespace FieldLink.PlcDrivers.XINJE
{
    /// <summary>XinJE 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class XinJEValueConverter
    {
        /// <summary>CalculateXC_D 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int CalculateXC_D(string address)
        {
            int add = Convert.ToInt32(address);
            if (add >= 8000)
                return add - 8000 + 0x4000;
            else
                return add;
        }
    }
}
