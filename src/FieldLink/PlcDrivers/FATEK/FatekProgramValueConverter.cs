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
using static FieldLink.PlcDrivers.FATEK.FatekProgramResponseParser;
using static FieldLink.PlcDrivers.FATEK.FatekProgramCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekControlCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekProgramControlCommandBuilder;
using static FieldLink.PlcDrivers.FATEK.FatekProgramControlResponseParser;

namespace FieldLink.PlcDrivers.FATEK
{
    /// <summary>FatekProgram 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class FatekProgramValueConverter
    {
        /// <summary>CalculateAcc 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string CalculateAcc(string data)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            int count = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                count += buffer[i];
            }

            return count.ToString("X4").Substring(2);
        }
    }
}
