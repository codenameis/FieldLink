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

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>Mitsubishi의 FxLinks 프로토콜 정보</summary>
    public class MelsecFxLinksAddress : DeviceAddress
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public MelsecFxLinksAddress()
        {
        }

        /// <summary>현재 주소 유형 정보</summary>
        public string TypeCode { get; set; }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        public override void Parse(string address, ushort length)
        {
            base.Parse(address, length);
        }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString()
        {
            switch (TypeCode)
            {
                case "X":
                case "Y":
                    return TypeCode + Convert.ToString(AddressStart, 8).PadLeft(AddressStart >= 10000 ? 6 : 4, '0');
                case "M":
                case "S":
                case "TS":
                case "TN":
                case "CS":
                case "CN":
                case "D":
                case "R":
                default:
                    return TypeCode + AddressStart.ToString("D" + ((AddressStart >= 10000 ? 7 : 5) - TypeCode.Length).ToString());
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<MelsecFxLinksAddress> ParseFrom(string address) => ParseFrom(address, 0);
        /// <summary>삼 FxLinks 프로토콜에서 실제 주소 정보를 분석합니다.</summary>
        /// <param name = "address">Mitsubishi 주소 정보</param>
        /// <param name = "length">읽을 길이 정보</param>
        /// <returns>분석 결과 정보</returns>
        public static OperationResult<MelsecFxLinksAddress> ParseFrom(string address, ushort length)
        {
            MelsecFxLinksAddress melsecFxLinks = new MelsecFxLinksAddress();
            melsecFxLinks.Length = length;
            try
            {
                switch (address[0])
                {
                    case 'X':
                    case 'x':
                    {
                        melsecFxLinks.AddressStart = Convert.ToUInt16(address.Substring(1), 8);
                        melsecFxLinks.TypeCode = "X";
                        break;
                    }

                    case 'Y':
                    case 'y':
                    {
                        melsecFxLinks.AddressStart = Convert.ToUInt16(address.Substring(1), 8);
                        melsecFxLinks.TypeCode = "Y";
                        break;
                    }

                    case 'M':
                    case 'm':
                    {
                        melsecFxLinks.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        melsecFxLinks.TypeCode = "M";
                        break;
                    }

                    case 'S':
                    case 's':
                    {
                        melsecFxLinks.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        melsecFxLinks.TypeCode = "S";
                        break;
                    }

                    case 'T':
                    case 't':
                    {
                        if (address[1] == 'S' || address[1] == 's')
                        {
                            melsecFxLinks.AddressStart = Convert.ToUInt16(address.Substring(2), 10);
                            melsecFxLinks.TypeCode = "TS";
                            break;
                        }
                        else if (address[1] == 'N' || address[1] == 'n')
                        {
                            melsecFxLinks.AddressStart = Convert.ToUInt16(address.Substring(2), 10);
                            melsecFxLinks.TypeCode = "TN";
                            break;
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }
                    }

                    case 'C':
                    case 'c':
                    {
                        if (address[1] == 'S' || address[1] == 's')
                        {
                            melsecFxLinks.AddressStart = Convert.ToUInt16(address.Substring(2), 10);
                            melsecFxLinks.TypeCode = "CS";
                            break;
                        }
                        else if (address[1] == 'N' || address[1] == 'n')
                        {
                            melsecFxLinks.AddressStart = Convert.ToUInt16(address.Substring(2), 10);
                            melsecFxLinks.TypeCode = "CN";
                            break;
                        }
                        else
                        {
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        }
                    }

                    case 'D':
                    case 'd':
                    {
                        melsecFxLinks.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        melsecFxLinks.TypeCode = "D";
                        break;
                    }

                    case 'R':
                    case 'r':
                    {
                        melsecFxLinks.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        melsecFxLinks.TypeCode = "R";
                        break;
                    }

                    default:
                        throw new Exception(ProtocolMessages.NotSupportedDataType);
                }

                return OperationResult.CreateSuccessResult(melsecFxLinks);
            }
            catch (Exception ex)
            {
                return new OperationResult<MelsecFxLinksAddress>("Address Create failed: " + ex.Message);
            }
        }
    }
}
