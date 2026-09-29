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
    /// <summary>Fuji SPH 주소 클래스 객체</summary>
    public class FujiSphAddress : DeviceAddress
    {
        /// <summary>데이터 타입 코드</summary>
        public byte TypeCode { get; set; }
        /// <summary>현재 주소의 비트 인덱스 정보</summary>
        public int BitIndex { get; set; }

        /// <summary>실제 후지 주소에서 주소 객체를 파싱합니다.</summary>
        /// <param name = "address">Fuji의 주소 데이터</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<FujiSphAddress> ParseFrom(string address)
        {
            return ParseFrom(address, 0);
        }

        /// <summary>실제 후지 주소에서 주소 객체를 파싱합니다.</summary>
        /// <param name = "address">Fuji의 주소 데이터</param>
        /// <param name = "length">읽을 데이터 길이</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<FujiSphAddress> ParseFrom(string address, ushort length)
        {
            FujiSphAddress addressData = new FujiSphAddress();
            try
            {
                switch (address[0])
                {
                    case 'M':
                    case 'm':
                    {
                        string[] splits = address.SplitDot();
                        int datablock = int.Parse(splits[0].Substring(1));
                        if (datablock == 0x01)
                            addressData.TypeCode = 0x02;
                        else if (datablock == 0x03)
                            addressData.TypeCode = 0x04;
                        else if (datablock == 0x0A)
                            addressData.TypeCode = 0x08;
                        else
                            throw new Exception(ProtocolMessages.NotSupportedDataType);
                        addressData.AddressStart = Convert.ToInt32(splits[1]);
                        if (splits.Length > 2)
                            addressData.BitIndex = AddressParameters.CalculateBitStartIndex(splits[2]);
                        break;
                    }

                    case 'Q':
                    case 'q':
                    case 'I':
                    case 'i':
                    {
                        string[] splits = address.SplitDot();
                        addressData.TypeCode = 0x01;
                        addressData.AddressStart = Convert.ToInt32(splits[0].Substring(1));
                        if (splits.Length > 1)
                            addressData.BitIndex = AddressParameters.CalculateBitStartIndex(splits[1]);
                        break;
                    }

                    default:
                        throw new Exception(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<FujiSphAddress>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(addressData);
        }
    }
}
