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
using static FieldLink.PlcDrivers.OpenProtocol.OpenProtocolNetCommandBuilder;
using static FieldLink.PlcDrivers.OpenProtocol.OpenProtocolNetResponseParser;

namespace FieldLink.PlcDrivers.OpenProtocol
{
    /// <summary>OpenProtocolNet 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class OpenProtocolNetValueConverter
    {
        /// <summary>DecideSubscribeData 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "mid">mid에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int DecideSubscribeData(int mid)
        {
            if (mid == 15 || mid == 35 || mid == 52 || mid == 61 || mid == 71 || mid == 74 || mid == 76 || mid == 91 || mid == 101)
                return mid + 1;
            if (mid == 106 || mid == 107)
                return 108;
            if (mid == 121 || mid == 122 || mid == 123 || mid == 124)
                return 125;
            if (mid == 152)
                return 153;
            if (mid == 211)
                return 212;
            if (mid == 217)
                return 218;
            if (mid == 221)
                return 222;
            if (mid == 242)
                return 243;
            if (mid == 251)
                return 252;
            if (mid == 401)
                return 402;
            if (mid == 421)
                return 422;
            return -1;
        }
    }
}
