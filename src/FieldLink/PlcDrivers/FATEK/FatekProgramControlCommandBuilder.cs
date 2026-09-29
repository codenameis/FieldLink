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
using static FieldLink.PlcDrivers.FATEK.FatekProgramValueConverter;
using static FieldLink.PlcDrivers.FATEK.FatekProgramResponseParser;
using static FieldLink.PlcDrivers.FATEK.FatekProgramCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekControlCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekProgramControlResponseParser;

namespace FieldLink.PlcDrivers.FATEK
{
    /// <summary>FatekProgramControl 요청 프레임을 생성합니다.</summary>
    public static class FatekProgramControlCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadStatus(byte station)
        {
            return PackFatekCommand(station, "40");
        }
    }
}
