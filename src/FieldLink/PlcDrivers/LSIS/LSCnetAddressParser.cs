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
using static FieldLink.PlcDrivers.LSIS.LSCnetResponseParser;
using static FieldLink.PlcDrivers.LSIS.LSCnetValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSCnetCommandBuilder;
using static FieldLink.PlcDrivers.LSIS.LSCnetDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSCnet 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class LSCnetAddressParser
    {
        /// <summary>GetAddressOfU_Q_I 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "Address">Address에 사용할 입력값입니다.</param>
        /// <param name = "IsWrite">IsWrite에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetAddressOfU_Q_I(string Address, bool IsWrite = false)
        {
            string[] Last = Address.Split('.');
            object result = 0;
            if (Last.Length >= 3)
            {
                int vSEQ = Convert.ToInt32(Last[2].Last().ToString(), 16);
                if (LSFastEnetValueConverter.IsHex(Last[2]) && IsWrite)
                {
                    result = (((int.Parse(Last[0]) * 32) + int.Parse(Last[1]))) + Last[2];
                }
                else
                {
                    result = (((int.Parse(Last[0]) * 32) + int.Parse(Last[1])) * 10) + vSEQ;
                }
            }
            else
            {
                result = ((int.Parse(Last[0]) * 32) + int.Parse(Last[1]));
            }

            return $"{result}";
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "transBit">transBit에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> AnalysisAddress(string address, bool transBit = false)
        {
            // Bit: P,M,L,K,F,T
            // Continus P,M,L,K,F,T,C,D,S
            StringBuilder sb = new StringBuilder();
            try
            {
                if (!CnetTypes.Contains(address[0]))
                    return new OperationResult<string>(ProtocolMessages.NotSupportedDataType);
                sb.Append("%");
                sb.Append(address[0]);
                if (address[1] == 'X')
                {
                    sb.Append("X");
                    if (address.IndexOf('.') > 0)
                    {
                        if (address[0] == 'U' || address[0] == 'I' || address[0] == 'Q')
                        {
                            sb.Append(GetAddressOfU_Q_I(address.Substring(2)));
                        }
                    }
                    else if (address[0] == 'I' || address[0] == 'Q')
                    {
                        sb.Append(address.Substring(2));
                    }
                    else if (transBit & address.IndexOf('.') > 0)
                    {
                        int bitIndex = AddressParameters.GetBitIndexInformation(ref address);
                        sb.Append(address.Substring(2));
                        sb.Append(bitIndex.ToString("X1"));
                    }
                    else
                    {
                        sb.Append(address.Substring(2));
                    }
                }
                else
                {
                    int index = 0;
                    int bitIndex = 0;
                    if (transBit & address.IndexOf('.') > 0)
                    {
                        sb.Append("X");
                        bitIndex = AddressParameters.GetBitIndexInformation(ref address);
                    }

                    switch (address[1])
                    {
                        case 'B':
                            sb.Append("B");
                            if (address.IndexOf('.') > 0)
                            {
                                if (address[0] == 'U' || address[0] == 'I' || address[0] == 'Q')
                                {
                                    sb.Append(GetAddressOfU_Q_I(address.Substring(2)));
                                }
                            }
                            else if (address[0] == 'I' || address[0] == 'Q')
                            {
                                sb.Append(address.Substring(2));
                            }
                            else
                            {
                                index = Convert.ToInt32(address.Substring(2)) * 2;
                            }

                            break;
                        case 'W':
                            sb.Append("W");
                            if (address.IndexOf('.') > 0)
                            {
                                if (address[0] == 'U' || address[0] == 'I' || address[0] == 'Q')
                                {
                                    sb.Append(GetAddressOfU_Q_I(address.Substring(2)));
                                }
                            }
                            else if (address[0] == 'I' || address[0] == 'Q')
                            {
                                sb.Append(address.Substring(2));
                            }
                            else
                            {
                                index = Convert.ToInt32(address.Substring(2)) * 2;
                            }

                            break;
                        case 'D':
                            sb.Append("D");
                            if (address.IndexOf('.') > 0)
                            {
                                if (address[0] == 'U' || address[0] == 'I' || address[0] == 'Q')
                                {
                                    sb.Append(GetAddressOfU_Q_I(address.Substring(2)));
                                }
                            }
                            else if (address[0] == 'I' || address[0] == 'Q')
                            {
                                sb.Append(address.Substring(2));
                            }
                            else
                            {
                                index = Convert.ToInt32(address.Substring(2)) * 4;
                            }

                            break;
                        case 'L':
                            sb.Append("L");
                            if (address.IndexOf('.') > 0)
                            {
                                if (address[0] == 'U' || address[0] == 'I' || address[0] == 'Q')
                                {
                                    sb.Append(GetAddressOfU_Q_I(address.Substring(2)));
                                }
                            }
                            else if (address[0] == 'I' || address[0] == 'Q')
                            {
                                sb.Append(address.Substring(2));
                            }
                            else
                            {
                                index = Convert.ToInt32(address.Substring(2)) * 8;
                            }

                            break;
                        default:
                            sb.Append("B");
                            if (address.IndexOf('.') > 0)
                            {
                                if (address[0] == 'U' || address[0] == 'I' || address[0] == 'Q')
                                {
                                    sb.Append(GetAddressOfU_Q_I(address.Substring(2)));
                                }
                            }
                            else if (address[0] == 'I' || address[0] == 'Q')
                            {
                                sb.Append(address.Substring(1));
                            }
                            else
                            {
                                index = Convert.ToInt32(address.Substring(1)) * 2;
                            }

                            break;
                    }

                    if (transBit)
                    {
                        sb.Append(index / 2);
                        sb.Append(bitIndex.ToString("X1"));
                    }
                    else
                    {
                        if (address[0] != 'U' || address[0] != 'I' || address[0] != 'Q')
                        {
                            sb.Append(index);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(sb.ToString());
        }
    }
}
