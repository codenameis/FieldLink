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

namespace FieldLink.PlcDrivers.Modbus
{
    /// <summary>ModbusAddressMapping 프로토콜 값입니다.</summary>
    public static class ModbusAddressMapping
    {
        /// <summary>TransAddressToModbus 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        /// <param name = "prase">prase에 사용할 입력값입니다.</param>
        /// <param name = "newAddress">newAddress에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool TransAddressToModbus(string station, string address, string[] code, int[] offset, Func<string, int> prase, out string newAddress)
        {
            newAddress = string.Empty;
            for (int i = 0; i < code.Length; i++)
            {
                if (address.StartsWithAndNumber(code[i]))
                {
                    newAddress = station + (prase(address.Substring(code[i].Length)) + offset[i]).ToString();
                    return true;
                }
            }

            return false;
        }

        /// <summary>TransAddressToModbus 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        /// <param name = "newAddress">newAddress에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool TransAddressToModbus(string station, string address, string[] code, int[] offset, out string newAddress)
        {
            return TransAddressToModbus(station, address, code, offset, int.Parse, out newAddress);
        }

        /// <summary>TransPointAddressToModbus 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        /// <param name = "prase">prase에 사용할 입력값입니다.</param>
        /// <param name = "newAddress">newAddress에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool TransPointAddressToModbus(string station, string address, string[] code, int[] offset, Func<string, int> prase, out string newAddress)
        {
            newAddress = string.Empty;
            int index = address.IndexOf('.');
            if (index > 0)
            {
                string tail = address.Substring(index);
                address = address.Substring(0, index);
                if (TransAddressToModbus(station, address, code, offset, prase, out newAddress))
                {
                    newAddress = newAddress + tail;
                    return true;
                }
            }

            return false;
        }

        /// <summary>TransPointAddressToModbus 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "station">대상 장치의 국번입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <param name = "offset">offset에 사용할 입력값입니다.</param>
        /// <param name = "newAddress">newAddress에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool TransPointAddressToModbus(string station, string address, string[] code, int[] offset, out string newAddress)
        {
            return TransPointAddressToModbus(station, address, code, offset, int.Parse, out newAddress);
        }
    }
}
