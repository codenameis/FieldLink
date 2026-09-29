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

namespace FieldLink.PlcDrivers.FATEK
{
    /// <summary>Fatek 프로그래밍 포트의 주소 클래스 객체</summary>
    public class FatekProgramAddress : DeviceAddress
    {
        /// <summary>데이터의 종류</summary>
        public string DataCode { get; set; }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        public override void Parse(string address, ushort length)
        {
            OperationResult<FatekProgramAddress> addressData = ParseFrom(address, length);
            if (!addressData.IsSuccess)
                throw new FormatException(addressData.Message);
            AddressStart = addressData.Content.AddressStart;
            Length = addressData.Content.Length;
            DataCode = addressData.Content.DataCode;
        }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString()
        {
            if (DataCode == "X" || DataCode == "Y" || DataCode == "M" || DataCode == "S" || DataCode == "T" || DataCode == "C" || DataCode == "RT" || DataCode == "RC")
                return DataCode + AddressStart.ToString("D4");
            else
                return DataCode + AddressStart.ToString("D5");
        }

        /// <summary>주소 문자열을 FATEK 프로그래밍 포트의 주소 정보로 변환합니다.</summary>
        /// <param name = "address">주소 정보</param>
        /// <param name = "length">데이터 길이</param>
        /// <returns>성공 여부</returns>
        public static OperationResult<FatekProgramAddress> ParseFrom(string address, ushort length)
        {
            try
            {
                FatekProgramAddress programAddress = new FatekProgramAddress();
                switch (address[0])
                {
                    case 'X':
                    case 'x':
                    {
                        programAddress.DataCode = "X";
                        programAddress.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'Y':
                    case 'y':
                    {
                        programAddress.DataCode = "Y";
                        programAddress.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'M':
                    case 'm':
                    {
                        programAddress.DataCode = "M";
                        programAddress.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'S':
                    case 's':
                    {
                        programAddress.DataCode = "S";
                        programAddress.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'T':
                    case 't':
                    {
                        programAddress.DataCode = "T";
                        programAddress.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'C':
                    case 'c':
                    {
                        programAddress.DataCode = "C";
                        programAddress.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'D':
                    case 'd':
                    {
                        programAddress.DataCode = "D";
                        programAddress.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        break;
                    }

                    case 'R':
                    case 'r':
                    {
                        if (address[1] == 'T' || address[1] == 't')
                        {
                            programAddress.DataCode = "RT";
                            programAddress.AddressStart = Convert.ToUInt16(address.Substring(2), 10);
                        }
                        else if (address[1] == 'C' || address[1] == 'c')
                        {
                            programAddress.DataCode = "RC";
                            programAddress.AddressStart = Convert.ToUInt16(address.Substring(2), 10);
                        }
                        else
                        {
                            programAddress.DataCode = "R";
                            programAddress.AddressStart = Convert.ToUInt16(address.Substring(1), 10);
                        }

                        break;
                    }

                    default:
                        throw new Exception(ProtocolMessages.NotSupportedDataType);
                }

                return OperationResult.CreateSuccessResult(programAddress);
            }
            catch (Exception ex)
            {
                return new OperationResult<FatekProgramAddress>(ex.Message);
            }
        }
    }
}
