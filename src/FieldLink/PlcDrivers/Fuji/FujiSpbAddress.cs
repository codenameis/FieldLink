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

namespace FieldLink.PlcDrivers.Fuji
{
    /// <summary>FujiSPB의 주소 정보, 데이터 타입, 초기 주소 동작을 수반할 수 있다.</summary>
    public class FujiSpbAddress : DeviceAddress
    {
        /// <summary>데이터 타입 코드</summary>
        public string TypeCode { get; set; }
        /// <summary>위치로 표시할 때 사용되는 정보</summary>
        public int BitIndex { get; set; }

        /// <summary>읽기/쓰기 데이터의 주소 정보 내용</summary>
        /// <returns>메시지 정보</returns>
        public string GetWordAddress()
        {
            return $"{TypeCode}{FujiSpbAddressParser.AnalysisIntegerAddress(AddressStart)}";
        }

        /// <summary>명령어를 가져와, 문자 주소로 누군가의 명령 내용을 입력합니다.</summary>
        /// <returns>메시지 정보</returns>
        public string GetWriteBoolAddress()
        {
            int byteIndex = AddressStart * 2;
            int bitIndex = BitIndex;
            if (bitIndex >= 8)
            {
                byteIndex++;
                bitIndex -= 8;
            }

            return $"{TypeCode}{FujiSpbAddressParser.AnalysisIntegerAddress(byteIndex)}{bitIndex:X2}";
        }

        /// <summary>관련 인덱스 정보를 비트 단위로 얻습니다.</summary>
        /// <returns>비트 데이터 정보</returns>
        public int GetBitIndex()
        {
            return AddressStart * 16 + BitIndex;
        }

        /// <summary>실제 후지 주소에서 주소 객체를 파싱합니다.</summary>
        /// <param name = "address">Fuji의 주소 데이터</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<FujiSpbAddress> ParseFrom(string address)
        {
            return ParseFrom(address, 0);
        }

        /// <summary>실제 후지 주소에서 주소 객체를 파싱합니다.</summary>
        /// <param name = "address">Fuji의 주소 데이터</param>
        /// <param name = "length">읽을 데이터 길이</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<FujiSpbAddress> ParseFrom(string address, ushort length)
        {
            FujiSpbAddress addressData = new FujiSpbAddress();
            addressData.Length = length;
            try
            {
                addressData.BitIndex = AddressParameters.GetBitIndexInformation(ref address);
                switch (address[0])
                {
                    case 'X':
                    case 'x':
                    {
                        addressData.TypeCode = "01";
                        addressData.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'Y':
                    case 'y':
                    {
                        addressData.TypeCode = "00";
                        addressData.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'M':
                    case 'm':
                    {
                        addressData.TypeCode = "02";
                        addressData.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'L':
                    case 'l':
                    {
                        addressData.TypeCode = "03";
                        addressData.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'T':
                    case 't':
                    {
                        if (address[1] == 'N' || address[1] == 'n')
                        {
                            addressData.TypeCode = "0A";
                            addressData.AddressStart = Convert.ToUInt16(address.Substring(2), 10);
                            break;
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            addressData.TypeCode = "04";
                            addressData.AddressStart = Convert.ToUInt16(address.Substring(2), 10);
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
                        if (address[1] == 'N' || address[1] == 'n')
                        {
                            addressData.TypeCode = "0B";
                            addressData.AddressStart = Convert.ToUInt16(address.Substring(2), 10);
                            break;
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            addressData.TypeCode = "05";
                            addressData.AddressStart = Convert.ToUInt16(address.Substring(2), 10);
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
                        addressData.TypeCode = "0C";
                        addressData.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'R':
                    case 'r':
                    {
                        addressData.TypeCode = "0D";
                        addressData.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'W':
                    case 'w':
                    {
                        addressData.TypeCode = "0E";
                        addressData.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    default:
                        throw new Exception(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<FujiSpbAddress>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(addressData);
        }
    }
}
