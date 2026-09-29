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

namespace FieldLink.PlcDrivers.Vigor
{
    /// <summary>포 PLC의 주소 클래스 객체</summary>
    public class VigorAddress : DeviceAddress
    {
        /// <summary>데이터를 가져오기 위한 코드, 또는 읽기를 대기하도록 설정</summary>
        public byte DataCode { get; set; }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString()
        {
            return AddressStart.ToString();
        }

        /// <summary>실제 Fujitsu PLC의 주소에서 주소 객체를 분석합니다.</summary>
        /// <param name = "address">풍부한 주소 데이터</param>
        /// <param name = "length">읽을 데이터 길이</param>
        /// <param name = "isBit">이 사이트가 방문되었는지</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<VigorAddress> ParseFrom(string address, ushort length, bool isBit)
        {
            VigorAddress addressData = new VigorAddress();
            try
            {
                addressData.Length = length;
                if (isBit)
                {
                    if (address.StartsWith("SM") || address.StartsWith("sm"))
                    {
                        addressData.DataCode = 0x94;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                    }
                    else if (address.StartsWith("TS") || address.StartsWith("ts"))
                    {
                        addressData.DataCode = 0x99;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                    } // 접점
                    else if (address.StartsWith("TC") || address.StartsWith("tc"))
                    {
                        addressData.DataCode = 0x98;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                    } // 코일
                    else if (address.StartsWith("CS") || address.StartsWith("cs"))
                    {
                        addressData.DataCode = 0x9D;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                    } // 접점
                    else if (address.StartsWith("CC") || address.StartsWith("cc"))
                    {
                        addressData.DataCode = 0x9C;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                    } // 코일
                    else if (address.StartsWith("X") || address.StartsWith("x"))
                    {
                        addressData.DataCode = 0x90;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                    }
                    else if (address.StartsWith("Y") || address.StartsWith("y"))
                    {
                        addressData.DataCode = 0x91;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                    }
                    else if (address.StartsWith("M") || address.StartsWith("m"))
                    {
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                        if (addressData.AddressStart >= 9000)
                        {
                            addressData.AddressStart = 0;
                            addressData.DataCode = 0x94;
                        }
                        else
                        {
                            addressData.DataCode = 0x92;
                        }
                    }
                    else if (address.StartsWith("S") || address.StartsWith("s"))
                    {
                        addressData.DataCode = 0x93;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                    }
                    else
                        return new OperationResult<VigorAddress>(ProtocolMessages.NotSupportedDataType);
                }
                else
                {
                    if (address.StartsWith("SD") || address.StartsWith("sd"))
                    {
                        addressData.DataCode = 0xA1;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                    }
                    else if (address.StartsWith("D") || address.StartsWith("d"))
                    {
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                        if (addressData.AddressStart >= 9000)
                        {
                            addressData.DataCode = 0xA1;
                            addressData.AddressStart = addressData.AddressStart - 9000;
                        }
                        else
                        {
                            addressData.DataCode = 0xA0;
                        }
                    }
                    else if (address.StartsWith("R") || address.StartsWith("r"))
                    {
                        addressData.DataCode = 0xA2;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                    }
                    else if (address.StartsWith("T") || address.StartsWith("t"))
                    {
                        addressData.DataCode = 0xA8;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                    }
                    else if (address.StartsWith("C") || address.StartsWith("c"))
                    {
                        addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                        if (addressData.AddressStart >= 200)
                        {
                            addressData.DataCode = 0xAD;
                        }
                        else
                        {
                            addressData.DataCode = 0xAC;
                        }
                    }
                    else
                        return new OperationResult<VigorAddress>(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<VigorAddress>(ProtocolMessages.AddressFormatWrong + ex.Message);
            }

            return OperationResult.CreateSuccessResult(addressData);
        }
    }
}
