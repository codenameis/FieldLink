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
using static FieldLink.PlcDrivers.Beckhoff.AdsCommandBuilder;
using static FieldLink.PlcDrivers.Beckhoff.AdsResponseParser;
using static FieldLink.PlcDrivers.Beckhoff.AdsValueConverter;

namespace FieldLink.PlcDrivers.Beckhoff
{
    /// <summary>Ads 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class AdsAddressParser
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
                return Convert.ToInt32(temp[0]) * 8 + AddressParameters.CalculateBitStartIndex(temp[1]);
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<uint, uint> AnalysisAddress(string address, bool isBit)
        {
            var result = new OperationResult<uint, uint>();
            try
            {
                if (address.StartsWith("i=") || address.StartsWith("I="))
                {
                    result.Content1 = 0xF005;
                    result.Content2 = uint.Parse(address.Substring(2));
                }
                else if (address.StartsWith("s=") || address.StartsWith("S="))
                {
                    result.Content1 = 0xF003;
                    result.Content2 = 0x0000;
                }
                else if (address.StartsWith("ig=") || address.StartsWith("IG="))
                {
                    address = address.ToUpper();
                    result.Content1 = (uint)AddressParameters.ExtractParameter(ref address, "IG", 0);
                    result.Content2 = uint.Parse(address);
                }
                else
                {
                    switch (address[0])
                    {
                        case 'M':
                        case 'm':
                        {
                            if (isBit)
                            {
                                result.Content1 = 0x4021;
                                result.Content2 = (uint)CalculateAddressStarted(address.Substring(1));
                            }
                            else
                            {
                                result.Content1 = 0x4020;
                                result.Content2 = uint.Parse(address.Substring(1));
                            }

                            break;
                        }

                        case 'I':
                        case 'i':
                        {
                            if (isBit)
                            {
                                result.Content1 = 0xF021;
                                result.Content2 = (uint)CalculateAddressStarted(address.Substring(1)) + 0x000FA000;
                            }
                            else
                            {
                                result.Content1 = 0xF020;
                                result.Content2 = uint.Parse(address.Substring(1)) + 0x0001F400;
                            }

                            break;
                        }

                        case 'Q':
                        case 'q':
                        {
                            if (isBit)
                            {
                                result.Content1 = 0xF031;
                                result.Content2 = (uint)CalculateAddressStarted(address.Substring(1)) + 0x001F4000;
                            }
                            else
                            {
                                result.Content1 = 0xF030;
                                result.Content2 = uint.Parse(address.Substring(1)) + 0x0003E800;
                            }

                            break;
                        }

                        default:
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                return result;
            }

            result.IsSuccess = true;
            result.Message = ProtocolMessages.SuccessText;
            return result;
        }
    }
}
