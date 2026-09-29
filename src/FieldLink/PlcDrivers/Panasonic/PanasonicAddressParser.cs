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
using static FieldLink.PlcDrivers.Panasonic.PanasonicValueConverter;
using static FieldLink.PlcDrivers.Panasonic.PanasonicCommandBuilder;
using static FieldLink.PlcDrivers.Panasonic.PanasonicResponseParser;

namespace FieldLink.PlcDrivers.Panasonic
{
    /// <summary>Panasonic 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class PanasonicAddressParser
    {
        /// <summary>CalculateComplexAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "fromBase">fromBase에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int CalculateComplexAddress(string address, int fromBase = 16)
        {
            int add = 0;
            if (address.IndexOf(".") < 0)
            {
                if (address.Length == 1)
                    add = Convert.ToInt32(address, fromBase);
                else
                    add = Convert.ToInt32(address.Substring(0, address.Length - 1)) * fromBase + Convert.ToInt32(address.Substring(address.Length - 1), fromBase);
            }
            else
            {
                add = Convert.ToInt32(address.Substring(0, address.IndexOf("."))) * fromBase;
                string bit = address.Substring(address.IndexOf(".") + 1);
                add += AddressParameters.CalculateBitStartIndex(bit);
            }

            return add;
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string, int> AnalysisAddress(string address)
        {
            var result = new OperationResult<string, int>();
            try
            {
                result.Content2 = 0;
                if (address.StartsWith("IX") || address.StartsWith("ix"))
                {
                    result.Content1 = "IX";
                    result.Content2 = int.Parse(address.Substring(2));
                }
                else if (address.StartsWith("IY") || address.StartsWith("iy"))
                {
                    result.Content1 = "IY";
                    result.Content2 = int.Parse(address.Substring(2));
                }
                else if (address.StartsWith("ID") || address.StartsWith("id"))
                {
                    result.Content1 = "ID";
                    result.Content2 = int.Parse(address.Substring(2));
                }
                else if (address.StartsWith("SR") || address.StartsWith("sr"))
                {
                    result.Content1 = "SR";
                    result.Content2 = CalculateComplexAddress(address.Substring(2));
                }
                else if (address.StartsWith("LD") || address.StartsWith("ld"))
                {
                    result.Content1 = "LD";
                    result.Content2 = int.Parse(address.Substring(2));
                }
                else if (address[0] == 'X' || address[0] == 'x')
                {
                    result.Content1 = "X";
                    result.Content2 = CalculateComplexAddress(address.Substring(1));
                }
                else if (address[0] == 'Y' || address[0] == 'y')
                {
                    result.Content1 = "Y";
                    result.Content2 = CalculateComplexAddress(address.Substring(1));
                }
                else if (address[0] == 'R' || address[0] == 'r')
                {
                    result.Content1 = "R";
                    result.Content2 = CalculateComplexAddress(address.Substring(1));
                }
                else if (address[0] == 'T' || address[0] == 't')
                {
                    result.Content1 = "T";
                    result.Content2 = int.Parse(address.Substring(1));
                }
                else if (address[0] == 'C' || address[0] == 'c')
                {
                    result.Content1 = "C";
                    result.Content2 = int.Parse(address.Substring(1));
                }
                else if (address[0] == 'L' || address[0] == 'l')
                {
                    result.Content1 = "L";
                    result.Content2 = CalculateComplexAddress(address.Substring(1));
                }
                else if (address[0] == 'D' || address[0] == 'd')
                {
                    result.Content1 = "D";
                    result.Content2 = int.Parse(address.Substring(1));
                }
                else if (address[0] == 'F' || address[0] == 'f')
                {
                    result.Content1 = "F";
                    result.Content2 = int.Parse(address.Substring(1));
                }
                else if (address[0] == 'S' || address[0] == 's')
                {
                    result.Content1 = "S";
                    result.Content2 = int.Parse(address.Substring(1));
                }
                else if (address[0] == 'K' || address[0] == 'k')
                {
                    result.Content1 = "K";
                    result.Content2 = int.Parse(address.Substring(1));
                }
                else
                {
                    throw new Exception(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                return result;
            }

            result.IsSuccess = true;
            return result;
        }
    }
}
