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
using static FieldLink.PlcDrivers.LSIS.LSFastEnetAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetCommandBuilder;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetResponseParser;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSFastEnet 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class LSFastEnetValueConverter
    {
        /// <summary>IsHex 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool IsHex(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            var state = false;
            for (var i = 0; i < value.Length; i++)
            {
                switch (value[i])
                {
                    case 'A':
                    case 'B':
                    case 'C':
                    case 'D':
                    case 'E':
                    case 'F':
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'e':
                    case 'f':
                        state = true;
                        break;
                }
            }

            return state;
        }
    }
}
