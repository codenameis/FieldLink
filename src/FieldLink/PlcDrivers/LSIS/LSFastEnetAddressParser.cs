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
using static FieldLink.PlcDrivers.LSIS.LSFastEnetValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetCommandBuilder;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetResponseParser;
using static FieldLink.PlcDrivers.LSIS.LSFastEnetDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSFastEnet 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class LSFastEnetAddressParser
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
                if (IsHex(Last[2]) && IsWrite)
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

        /// <summary>CalculateAddressStarted 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "QI">QI에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int CalculateAddressStarted(string address, bool QI = false)
        {
            if (address.IndexOf('.') < 0)
            {
                return Convert.ToInt32(address);
            }
            else
            {
                string[] temp = address.Split('.');
                if (!QI)
                    return Convert.ToInt32(temp[0]);
                else if (temp.Length >= 4)
                    return Convert.ToInt32(temp[3]);
                else
                    return Convert.ToInt32(temp[2]);
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> AnalysisAddress(string address)
        {
            // P,M,L,K,F,T
            // P,M,L,K,F,T,C,D,S,U
            StringBuilder sb = new StringBuilder();
            try
            {
                sb.Append("%");
                bool exsist = false;
                for (int i = 0; i < AddressTypes.Length; i++)
                {
                    if (AddressTypes[i] == address[0])
                    {
                        sb.Append(AddressTypes[i]);
                        switch (address[1])
                        {
                            case 'X':
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
                                    sb.Append(CalculateAddressStarted(address.Substring(2, address.Length - 2), true));
                                }
                                else
                                {
                                    if (IsHex(address.Substring(2)))
                                    {
                                        sb.Append(address.Substring(2));
                                    }
                                    else
                                        sb.Append(CalculateAddressStarted(address.Substring(2)));
                                }

                                break;
                            default:
                                int startIndex = 0;
                                if (address[1] == 'B')
                                {
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
                                        startIndex = CalculateAddressStarted(address.Substring(2), true);
                                    }
                                    else
                                    {
                                        startIndex = CalculateAddressStarted(address.Substring(2));
                                    }

                                    sb.Append(startIndex);
                                }
                                else if (address[1] == 'W')
                                {
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
                                        startIndex = CalculateAddressStarted(address.Substring(2), true);
                                    }
                                    else
                                    {
                                        startIndex = CalculateAddressStarted(address.Substring(2)) * 2;
                                    }

                                    sb.Append(startIndex);
                                }
                                else if (address[1] == 'D')
                                {
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
                                        startIndex = CalculateAddressStarted(address.Substring(2)) * 4;
                                        sb.Append(startIndex);
                                    }
                                }
                                else if (address[1] == 'L')
                                {
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
                                        startIndex = CalculateAddressStarted(address.Substring(2)) * 8;
                                        sb.Append(startIndex);
                                    }
                                }
                                else
                                {
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
                                        sb.Append(CalculateAddressStarted(address.Substring(1), true));
                                    }
                                    else
                                    {
                                        if (IsHex(address.Substring(1)))
                                        {
                                            sb.Append(address.Substring(1));
                                        }
                                        else
                                            sb.Append(CalculateAddressStarted(address.Substring(1)) * 2);
                                    }
                                }

                                break;
                        }

                        exsist = true;
                        break;
                    }
                }

                if (!exsist)
                    throw new Exception(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(sb.ToString());
        }

        /// <summary>GetDataTypeToAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> GetDataTypeToAddress(string address)
        {
            string lSDataType = string.Empty;
            ;
            try
            {
                bool exsist = false;
                for (int i = 0; i < AddressTypes.Length; i++)
                {
                    if (AddressTypes[i] == address[0])
                    {
                        switch (address[1])
                        {
                            case 'X':
                                lSDataType = "Bit";
                                break;
                            case 'W':
                                lSDataType = "Word";
                                break;
                            case 'D':
                                lSDataType = "DWord";
                                break;
                            case 'L':
                                lSDataType = "LWord";
                                break;
                            case 'B':
                                lSDataType = "Continuous";
                                break;
                            default:
                                lSDataType = "Continuous";
                                break;
                        }

                        exsist = true;
                        break;
                    }
                }

                if (!exsist)
                    throw new Exception(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<string>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(lSDataType);
        }
    }
}
