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

namespace FieldLink.PlcDrivers.Toyota
{
    /// <summary>토요타 공작기 PLC의 주소형 객체</summary>
    public class ToyoPucAddress : DeviceAddress
    {
        /// <summary>읽을 주소의 PRG 번호를 가져오거나 설정합니다. 0보다 작으면 사용하지 않습니다.</summary>
        public int PRG { get; set; } = -1;

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString()
        {
            return AddressStart.ToString();
        }

        /// <summary>실제 토요타 PLC의 주소에서 주소 객체를 파싱</summary>
        /// <param name = "address">도요타의 주소 데이터 정보</param>
        /// <param name = "length">읽을 데이터 길이</param>
        /// <param name = "isBit">이 사이트가 방문되었는지</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<ToyoPucAddress> ParseFrom(string address, ushort length, bool isBit)
        {
            ToyoPucAddress addressData = new ToyoPucAddress();
            addressData.Length = length;
            addressData.PRG = AddressParameters.ExtractParameter(ref address, "prg", -1);
            try
            {
                if (address[0] == 'K' || address[0] == 'k')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + (isBit ? 0x0200 : 0x020);
                else if (address[0] == 'V' || address[0] == 'v')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + (isBit ? 0x0500 : 0x050);
                else if (address[0] == 'T' || address[0] == 't')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + (isBit ? 0x0600 : 0x060);
                else if (address[0] == 'C' || address[0] == 'c')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + (isBit ? 0x0600 : 0x060);
                else if (address[0] == 'L' || address[0] == 'l')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + (isBit ? 0x0800 : 0x080);
                else if (address[0] == 'X' || address[0] == 'x')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + (isBit ? 0x1000 : 0x100);
                else if (address[0] == 'Y' || address[0] == 'y')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + (isBit ? 0x1000 : 0x100);
                else if (address[0] == 'M' || address[0] == 'm')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + (isBit ? 0x1800 : 0x180);
                // 읽기/쓰기 영역
                else if (address[0] == 'S' || address[0] == 's')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + 0x0200;
                else if (address[0] == 'N' || address[0] == 'n')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + 0x0600;
                else if (address[0] == 'R' || address[0] == 'r')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + 0x0800;
                else if (address[0] == 'D' || address[0] == 'd')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + 0x1000;
                else if (address[0] == 'B' || address[0] == 'b')
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + 0x6000;
                // 확장 지역
                else if (address[0] == 'E' || address[0] == 'e')
                {
                    if (address[1] == 'K' || address[1] == 'k')
                    {
                        addressData.PRG = 0x00;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + (isBit ? 0x1000 : 0x100);
                    }
                    else if (address[1] == 'V' || address[1] == 'v')
                    {
                        addressData.PRG = 0x00;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + (isBit ? 0x2000 : 0x200);
                    }
                    else if (address[1] == 'T' || address[1] == 't')
                    {
                        addressData.PRG = 0x00;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + (isBit ? 0x3000 : 0x300);
                    }
                    else if (address[1] == 'C' || address[1] == 'c')
                    {
                        addressData.PRG = 0x00;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + (isBit ? 0x3000 : 0x300);
                    }
                    else if (address[1] == 'L' || address[1] == 'l')
                    {
                        addressData.PRG = 0x00;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + (isBit ? 0x3800 : 0x380);
                    }
                    else if (address[1] == 'X' || address[1] == 'x')
                    {
                        addressData.PRG = 0x00;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + (isBit ? 0x5800 : 0x580);
                    }
                    else if (address[1] == 'Y' || address[1] == 'y')
                    {
                        addressData.PRG = 0x00;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + (isBit ? 0x5800 : 0x580);
                    }
                    else if (address[1] == 'M' || address[1] == 'm')
                    {
                        addressData.PRG = 0x00;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + (isBit ? 0x6000 : 0x600);
                    }
                    // 확장 워드
                    else if (address[1] == 'S' || address[1] == 's')
                    {
                        addressData.PRG = 0x00;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + 0x800;
                    }
                    else if (address[1] == 'N' || address[1] == 'n')
                    {
                        addressData.PRG = 0x00;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + 0x1000;
                    }
                    else if (address[1] == 'B' || address[1] == 'b')
                    {
                        int add = Convert.ToInt32(address.Substring(2), 16);
                        if (add < 0x8000)
                        {
                            addressData.PRG = 0x09;
                            addressData.AddressStart = add;
                        }
                        else if (add < 0x10000)
                        {
                            addressData.PRG = 0x0A;
                            addressData.AddressStart = add - 0x8000;
                        }
                        else
                        {
                            addressData.PRG = 0x0B;
                            addressData.AddressStart = add - 0x10000;
                        }
                    }
                    else
                        throw new Exception(ProtocolMessages.NotSupportedDataType);
                }
                else if (address[0] == 'H' || address[0] == 'h')
                {
                    addressData.PRG = 0x00;
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16) + 0x1800;
                }
                else if (address[0] == 'U' || address[0] == 'u')
                {
                    addressData.PRG = 0x08;
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1), 16);
                }
                else if (address[0] == 'G' || address[0] == 'g')
                {
                    if (address[1] == 'X' || address[1] == 'x')
                    {
                        addressData.PRG = 0x07;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16);
                    }
                    else if (address[1] == 'Y' || address[1] == 'y')
                    {
                        addressData.PRG = 0x07;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16);
                    }
                    else if (address[1] == 'M' || address[1] == 'm')
                    {
                        addressData.PRG = 0x07;
                        addressData.AddressStart = Convert.ToInt32(address.Substring(2), 16) + 0x1000;
                    }
                    else
                        throw new Exception(ProtocolMessages.NotSupportedDataType);
                }
                else
                    throw new Exception(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<ToyoPucAddress>(ex.Message);
            }

            return OperationResult.CreateSuccessResult(addressData);
        }
    }
}
