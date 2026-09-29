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
using static FieldLink.PlcDrivers.FATEK.FatekProgramControlCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekProgramControlResponseParser;

namespace FieldLink.PlcDrivers.FATEK
{
    /// <summary>FatekControl 요청 프레임을 생성합니다.</summary>
    public static class FatekControlCommandBuilder
    {
        /// <summary>운전 시작 명령 프레임을 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildRun(byte station) => PackFatekCommand(station, "411");
        /// <summary>운전 정지 명령 프레임을 생성합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildStop(byte station) => PackFatekCommand(station, "410");
    }
}
