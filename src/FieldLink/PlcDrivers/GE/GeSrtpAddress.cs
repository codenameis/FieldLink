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

namespace FieldLink.PlcDrivers.GE
{
    /// <summary>GE의 SRTP 프로토콜의 주소 내용, 주로 데이터 코드 정보를 포함하고, 정적인 주소 파싱 메서드</summary>
    public class GeSrtpAddress : DeviceAddress
    {
        /// <summary>데이터를 가져오기 위한 코드, 또는 읽기를 대기하도록 설정</summary>
        public byte DataCode { get; set; }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        public override void Parse(string address, ushort length)
        {
            OperationResult<GeSrtpAddress> addressData = ParseFrom(address, length, false);
            if (!addressData.IsSuccess)
                throw new FormatException(addressData.Message);
            AddressStart = addressData.Content.AddressStart;
            Length = addressData.Content.Length;
            DataCode = addressData.Content.DataCode;
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<GeSrtpAddress> ParseFrom(string address, bool isBit)
        {
            return ParseFrom(address, 0, isBit);
        }

        /// <summary>그리고 실제 데이터 코드가 있는 GE의 주소에서 <c>GeSRTPAddress</c> 주소 정보, 시작 주소 자동으로 1을 빼고 실제 주소와 일치합니다.</summary>
        /// <param name = "address">실제 주소 데이터</param>
        /// <param name = "length">읽을 길이 정보</param>
        /// <param name = "isBit">비트 연산 여부</param>
        /// <returns>성공 여부 GE 주소 대상</returns>
        public static OperationResult<GeSrtpAddress> ParseFrom(string address, ushort length, bool isBit)
        {
            GeSrtpAddress addressData = new GeSrtpAddress();
            try
            {
                addressData.Length = length;
                if (address.StartsWith("AI") || address.StartsWith("ai"))
                {
                    if (isBit)
                        return new OperationResult<GeSrtpAddress>(ProtocolMessages.GeSRTPNotSupportBitReadWrite);
                    addressData.DataCode = 0x0A;
                    addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                }
                else if (address.StartsWith("AQ") || address.StartsWith("aq"))
                {
                    if (isBit)
                        return new OperationResult<GeSrtpAddress>(ProtocolMessages.GeSRTPNotSupportBitReadWrite);
                    addressData.DataCode = 0x0C;
                    addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                }
                else if (address.StartsWith("R") || address.StartsWith("r"))
                {
                    if (isBit)
                        return new OperationResult<GeSrtpAddress>(ProtocolMessages.GeSRTPNotSupportBitReadWrite);
                    addressData.DataCode = 0x08;
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                }
                else if (address.StartsWith("SA") || address.StartsWith("sa"))
                {
                    addressData.DataCode = isBit ? (byte)0x4E : (byte)0x18;
                    addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                }
                else if (address.StartsWith("SB") || address.StartsWith("sb"))
                {
                    addressData.DataCode = isBit ? (byte)0x50 : (byte)0x1A;
                    addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                }
                else if (address.StartsWith("SC") || address.StartsWith("sc"))
                {
                    addressData.DataCode = isBit ? (byte)0x52 : (byte)0x1C;
                    addressData.AddressStart = Convert.ToInt32(address.Substring(2));
                }
                else
                {
                    if (address[0] == 'I' || address[0] == 'i')
                        addressData.DataCode = isBit ? (byte)0x46 : (byte)0x10;
                    else if (address[0] == 'Q' || address[0] == 'q')
                        addressData.DataCode = isBit ? (byte)0x48 : (byte)0x12;
                    else if (address[0] == 'M' || address[0] == 'm')
                        addressData.DataCode = isBit ? (byte)0x4C : (byte)0x16;
                    else if (address[0] == 'T' || address[0] == 't')
                        addressData.DataCode = isBit ? (byte)0x4A : (byte)0x14;
                    else if (address[0] == 'S' || address[0] == 's')
                        addressData.DataCode = isBit ? (byte)0x54 : (byte)0x1E;
                    else if (address[0] == 'G' || address[0] == 'g')
                        addressData.DataCode = isBit ? (byte)0x56 : (byte)0x38;
                    else
                        throw new Exception(ProtocolMessages.NotSupportedDataType);
                    addressData.AddressStart = Convert.ToInt32(address.Substring(1));
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<GeSrtpAddress>(ex.Message);
            }

            if (addressData.AddressStart == 0)
                return new OperationResult<GeSrtpAddress>(ProtocolMessages.GeSRTPAddressCannotBeZero);
            if (addressData.AddressStart > 0)
                addressData.AddressStart--;
            return OperationResult.CreateSuccessResult(addressData);
        }
    }
}
