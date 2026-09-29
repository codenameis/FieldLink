using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Panasonic;
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
using static FieldLink.PlcDrivers.LSIS.LSisServerValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSisServerDefinitions;
using static FieldLink.PlcDrivers.LSIS.LSisServerCommandBuilder;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSisServer 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class LSisServerAddressParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int CheckAddress(string address)
        {
            int bitSelacdetAddress = 0;
            if (IsHex(address))
            {
                int v;
                if (Int32.TryParse(address, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out v))
                    bitSelacdetAddress = v;
            }
            else
            {
                bitSelacdetAddress = int.Parse(address);
            }

            return bitSelacdetAddress;
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static ushort AnalysisAddressLength(string address)
        {
            switch (address[1])
            {
                case 'X':
                    return 1;
                case 'B':
                    return 1;
                case 'W':
                    return 2;
                case 'D':
                    return 4;
                case 'L':
                    return 8;
                default:
                    return 1;
            }
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string> AnalysisAddressToByteUnit(string address, bool isBit)
        {
            if (!LSFastEnetDefinitions.AddressTypes.Contains(address.Substring(0, 1)))
                return new OperationResult<string>(ProtocolMessages.NotSupportedDataType);
            int addressStart;
            try
            {
                if (address[0] == 'D' || address[0] == 'T')
                {
                    switch (address[1])
                    {
                        case 'B':
                            addressStart = Convert.ToInt32(address.Substring(2));
                            break;
                        case 'W':
                            addressStart = Convert.ToInt32(address.Substring(2)) * 2;
                            break;
                        case 'D':
                            addressStart = Convert.ToInt32(address.Substring(2)) * 4;
                            break;
                        case 'L':
                            addressStart = Convert.ToInt32(address.Substring(2)) * 8;
                            break;
                        default:
                            addressStart = Convert.ToInt32(address.Substring(1)) * 2;
                            break;
                    }
                }
                else
                {
                    if (isBit)
                    {
                        switch (address[1])
                        {
                            case 'X':
                                addressStart = PanasonicAddressParser.CalculateComplexAddress(address.Substring(2));
                                break;
                            default:
                                addressStart = PanasonicAddressParser.CalculateComplexAddress(address.Substring(1));
                                break;
                        }
                    }
                    else
                    {
                        switch (address[1])
                        {
                            case 'X':
                                addressStart = Convert.ToInt32(address.Substring(2));
                                break;
                            case 'B':
                                addressStart = Convert.ToInt32(address.Substring(2));
                                break;
                            case 'W':
                                addressStart = Convert.ToInt32(address.Substring(2)) * 2;
                                break;
                            case 'D':
                                addressStart = Convert.ToInt32(address.Substring(2)) * 4;
                                break;
                            case 'L':
                                addressStart = Convert.ToInt32(address.Substring(2)) * 8;
                                break;
                            default:
                                addressStart = Convert.ToInt32(address.Substring(1)) * (isBit ? 1 : 2);
                                break;
                        }
                    }
                }

                return OperationResult.CreateSuccessResult(address.Substring(0, 1) + addressStart.ToString());
            }
            catch (Exception ex)
            {
                return new OperationResult<string>("AnalysisAddress Failed: " + ex.Message + " Source: " + address);
            }
        }
    }
}
