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
using static FieldLink.PlcDrivers.Siemens.SiemensFetchWriteNetResponseParser;
using static FieldLink.PlcDrivers.Siemens.SiemensFetchWriteNetCommandBuilder;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensFetchWriteNet 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class SiemensFetchWriteNetAddressParser
    {
        /// <summary>CalculateAddressStarted 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static int CalculateAddressStarted(string address)
        {
            if (address.IndexOf('.') < 0)
            {
                return Convert.ToInt32(address);
            }
            else
            {
                string[] temp = address.Split('.');
                return Convert.ToInt32(temp[0]);
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static OperationResult<byte, int, ushort> AnalysisAddress(string address)
        {
            var result = new OperationResult<byte, int, ushort>();
            try
            {
                result.Content3 = 0;
                if (address[0] == 'I')
                {
                    result.Content1 = 0x03;
                    result.Content2 = CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'Q')
                {
                    result.Content1 = 0x04;
                    result.Content2 = CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'M')
                {
                    result.Content1 = 0x02;
                    result.Content2 = CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'D' || address.Substring(0, 2) == "DB")
                {
                    result.Content1 = 0x01;
                    string[] adds = address.Split('.');
                    if (address[1] == 'B')
                    {
                        result.Content3 = Convert.ToUInt16(adds[0].Substring(2));
                    }
                    else
                    {
                        result.Content3 = Convert.ToUInt16(adds[0].Substring(1));
                    }

                    if (result.Content3 > 255)
                    {
                        result.Message = ProtocolMessages.SiemensDBAddressNotAllowedLargerThan255;
                        return result;
                    }

                    result.Content2 = CalculateAddressStarted(address.Substring(address.IndexOf('.') + 1));
                }
                else if (address[0] == 'T')
                {
                    result.Content1 = 0x07;
                    result.Content2 = CalculateAddressStarted(address.Substring(1));
                }
                else if (address[0] == 'C')
                {
                    result.Content1 = 0x06;
                    result.Content2 = CalculateAddressStarted(address.Substring(1));
                }
                else
                {
                    result.Message = ProtocolMessages.NotSupportedDataType;
                    result.Content1 = 0;
                    result.Content2 = 0;
                    result.Content3 = 0;
                    return result;
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
