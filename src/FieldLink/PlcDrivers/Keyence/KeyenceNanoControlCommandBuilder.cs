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
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoCommandBuilder;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoAddressParser;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoResponseParser;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoControlResponseParser;

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>KeyenceNanoControl 요청 프레임을 생성합니다.</summary>
    public static class KeyenceNanoControlCommandBuilder
    {
        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadPlcType()
        {
            return Encoding.ASCII.GetBytes("?K\r");
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadPlcMode()
        {
            return Encoding.ASCII.GetBytes("?M\r");
        }

        /// <summary>쓰기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "dateTime">dateTime에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildSetPlcDateTime(DateTime dateTime)
        {
            return Encoding.ASCII.GetBytes($"WRT {dateTime.Year - 2000:D2} {dateTime.Month:D2} {dateTime.Day:D2} " + $"{dateTime.Hour:D2} {dateTime.Minute:D2} {dateTime.Second:D2} {(int)dateTime.DayOfWeek}\r");
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadAddressAnnotation(string address)
        {
            return Encoding.ASCII.GetBytes($"RDC {address}\r");
        }

        /// <summary>읽기 요청의 명령 바이트를 생성합니다.</summary>
        /// <param name = "unit">unit에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static byte[] BuildReadExpansionMemory(byte unit, ushort address, ushort length)
        {
            return Encoding.ASCII.GetBytes($"URD {unit} {address}.U {length}\r");
        }
    }
}
