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
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyCommandBuilder;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyResponseParser;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyDefinitions;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradley 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class AllenBradleyAddressParser
    {
        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "pathCommand">pathCommand에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<int, int> ParseRequestPathSymbolInstanceAddressing(byte[] pathCommand)
        {
            int classid = 0;
            int instanceid = 0;
            int index = 0;
            if (pathCommand[index] == 0x20)
            {
                index++;
                classid = pathCommand[index++];
            }
            else if (pathCommand[index] == 0x21)
            {
                index += 2;
                classid = BitConverter.ToUInt16(pathCommand, index);
                index += 2;
            }
            else
            {
                return new OperationResult<int, int>();
            }

            if (pathCommand[index] == 0x24)
            {
                index++;
                instanceid = pathCommand[index++];
            }
            else if (pathCommand[index] == 0x25)
            {
                index += 2;
                instanceid = BitConverter.ToUInt16(pathCommand, index);
                index += 2;
            }
            else
            {
                return new OperationResult<int, int>();
            }

            return OperationResult.CreateSuccessResult(classid, instanceid);
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "arrayIndex">arrayIndex에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string AnalysisArrayIndex(string address, out int arrayIndex)
        {
            arrayIndex = 0;
            if (!address.EndsWith("]"))
                return address;
            int index = address.LastIndexOf('[');
            if (index < 0)
                return address;
            address = address.Remove(address.Length - 1);
            try
            {
                arrayIndex = int.Parse(address.Substring(index + 1));
                address = address.Substring(0, index);
                return address;
            }
            catch
            {
                return address;
            }
        }
    }
}
