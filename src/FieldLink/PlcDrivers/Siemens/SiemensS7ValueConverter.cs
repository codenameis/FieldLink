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
using static FieldLink.PlcDrivers.Siemens.SiemensS7ResponseParser;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensS7 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class SiemensS7ValueConverter
    {
        /// <summary>ArraySplitByLength 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "s7Addresses">s7Addresses에 사용할 입력값입니다.</param>
        /// <param name = "pduLength">pduLength에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static List<S7DeviceAddress[]> ArraySplitByLength(S7DeviceAddress[] s7Addresses, int pduLength)
        {
            List<S7DeviceAddress[]> array = new List<S7DeviceAddress[]>();
            List<S7DeviceAddress> tmpArray = new List<S7DeviceAddress>();
            int lengthTotle = 0;
            for (int i = 0; i < s7Addresses.Length; i++)
            {
                if (tmpArray.Count >= 19 || (lengthTotle + s7Addresses[i].Length) >= pduLength)
                {
                    if (tmpArray.Count > 0)
                    {
                        array.Add(tmpArray.ToArray());
                        tmpArray.Clear();
                    }

                    lengthTotle = 0;
                }

                tmpArray.Add(s7Addresses[i]);
                lengthTotle += s7Addresses[i].Length;
            }

            if (tmpArray.Count > 0)
                array.Add(tmpArray.ToArray());
            return array;
        }
    }
}
