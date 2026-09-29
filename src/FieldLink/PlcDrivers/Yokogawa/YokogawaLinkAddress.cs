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

namespace FieldLink.PlcDrivers.Yokogawa
{
    /// <summary>요카오 PLC의 주소 표시 클래스</summary>
    public class YokogawaLinkAddress : DeviceAddress
    {
        /// <summary>데이터를 가져오기 위한 코드, 또는 읽기를 대기하도록 설정</summary>
        public int DataCode { get; set; }

        /// <summary>현재 요카오 PLC의 주소를 얻는 바이너리 표현 방법</summary>
        /// <returns>이진 데이터 정보</returns>
        public byte[] GetAddressBinaryContent()
        {
            byte[] buffer = new byte[6];
            buffer[0] = BitConverter.GetBytes(DataCode)[1];
            buffer[1] = BitConverter.GetBytes(DataCode)[0];
            buffer[2] = BitConverter.GetBytes(AddressStart)[3];
            buffer[3] = BitConverter.GetBytes(AddressStart)[2];
            buffer[4] = BitConverter.GetBytes(AddressStart)[1];
            buffer[5] = BitConverter.GetBytes(AddressStart)[0];
            return buffer;
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        public override void Parse(string address, ushort length)
        {
            OperationResult<YokogawaLinkAddress> addressData = ParseFrom(address, length);
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
            switch (DataCode)
            {
                case 0x31:
                    return "CN" + AddressStart.ToString();
                case 0x21:
                    return "TN" + AddressStart.ToString();
                case 0x18:
                    return "X" + AddressStart.ToString();
                case 0x19:
                    return "Y" + AddressStart.ToString();
                case 0x09:
                    return "I" + AddressStart.ToString();
                case 0x05:
                    return "E" + AddressStart.ToString();
                case 0x0D:
                    return "M" + AddressStart.ToString();
                case 0x14:
                    return "T" + AddressStart.ToString();
                case 0x03:
                    return "C" + AddressStart.ToString();
                case 0x0C:
                    return "L" + AddressStart.ToString();
                case 0x04:
                    return "D" + AddressStart.ToString();
                case 0x02:
                    return "B" + AddressStart.ToString();
                case 0x06:
                    return "F" + AddressStart.ToString();
                case 0x12:
                    return "R" + AddressStart.ToString();
                case 0x16:
                    return "V" + AddressStart.ToString();
                case 0x1A:
                    return "Z" + AddressStart.ToString();
                case 0x17:
                    return "W" + AddressStart.ToString();
                default:
                    return AddressStart.ToString();
            }
        }

        /// <summary>주소 문자열을 Yokogawa Link 프로토콜의 주소 정보로 변환합니다.</summary>
        /// <param name = "address">주소 정보</param>
        /// <param name = "length">데이터 길이</param>
        /// <returns>성공 여부</returns>
        public static OperationResult<YokogawaLinkAddress> ParseFrom(string address, ushort length)
        {
            try
            {
                int type = 0;
                int offset = 0;
                if (address.StartsWith("CN") || address.StartsWith("cn"))
                {
                    type = 0x31;
                    offset = int.Parse(address.Substring(2));
                }
                else if (address.StartsWith("TN") || address.StartsWith("tn"))
                {
                    type = 0x21;
                    offset = int.Parse(address.Substring(2));
                }
                else if (address.StartsWith("X") || address.StartsWith("x"))
                {
                    type = 0x18;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("Y") || address.StartsWith("y"))
                {
                    type = 0x19;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("I") || address.StartsWith("i"))
                {
                    type = 0x09;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("E") || address.StartsWith("e"))
                {
                    type = 0x05;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("M") || address.StartsWith("m"))
                {
                    type = 0x0D;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("T") || address.StartsWith("t"))
                {
                    type = 0x14;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("C") || address.StartsWith("c"))
                {
                    type = 0x03;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("L") || address.StartsWith("l"))
                {
                    type = 0x0C;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("D") || address.StartsWith("d"))
                {
                    type = 0x04;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("B") || address.StartsWith("b"))
                {
                    type = 0x02;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("F") || address.StartsWith("f"))
                {
                    type = 0x06;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("R") || address.StartsWith("r"))
                {
                    type = 0x12;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("V") || address.StartsWith("v"))
                {
                    type = 0x16;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("Z") || address.StartsWith("z"))
                {
                    type = 0x1A;
                    offset = int.Parse(address.Substring(1));
                }
                else if (address.StartsWith("W") || address.StartsWith("w"))
                {
                    type = 0x17;
                    offset = int.Parse(address.Substring(1));
                }
                else
                {
                    throw new Exception(ProtocolMessages.NotSupportedDataType);
                }

                return OperationResult.CreateSuccessResult(new YokogawaLinkAddress() { DataCode = type, AddressStart = offset, Length = length });
            }
            catch (Exception ex)
            {
                return new OperationResult<YokogawaLinkAddress>(ex.Message);
            }
        }
    }
}
