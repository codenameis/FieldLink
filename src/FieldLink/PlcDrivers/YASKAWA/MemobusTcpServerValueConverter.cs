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
using static FieldLink.PlcDrivers.YASKAWA.MemobusTcpServerCommandBuilder;
using static FieldLink.PlcDrivers.YASKAWA.MemobusTcpServerDefinitions;

namespace FieldLink.PlcDrivers.YASKAWA
{
    /// <summary>MemobusTcpServer 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class MemobusTcpServerValueConverter
    {
        /// <summary>TransByteHighLow 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "value">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte TransByteHighLow(byte value)
        {
            int tmp = value & 0xf0;
            tmp >>= 4;
            return (byte)(((value & 0x0f) << 4) | tmp);
        }
    }
}
