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
    /// <summary>Fuji CommandSettingsType의 프로토콜 정보</summary>
    public class FujiCommandSettingTypeAddress : DeviceAddress
    {
        /// <summary>데이터의 코드 정보</summary>
        public byte DataCode { get; set; }
        /// <summary>주소 헤드 정보, 버퍼 상태</summary>
        public string AddressHeader { get; set; }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "length">요청할 데이터의 길이입니다.</param>
        public override void Parse(string address, ushort length)
        {
            base.Parse(address, length);
        }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString() => AddressHeader + AddressStart;
        /// <summary>문자열 주소에서 fuji의 실제 주소 정보를 파싱하고, 만약 파싱이 성공하면 <c>OperateResult.IsSuccess</c> TRUE, 가져오기 <c>OperateResult{T}.Content</c> 값은</summary>
        /// <param name = "address">문자열 주소</param>
        /// <param name = "length">읽을 길이 정보</param>
        /// <returns>파싱 성공</returns>
        public static OperationResult<FujiCommandSettingTypeAddress> ParseFrom(string address, ushort length)
        {
            try
            {
                FujiCommandSettingTypeAddress fujiAddress = new FujiCommandSettingTypeAddress();
                string addType = string.Empty;
                string addOffset = string.Empty;
                if (address.IndexOf('.') < 0)
                {
                    Match match = Regex.Match(address, "^[A-Z]+");
                    if (!match.Success)
                        return new OperationResult<FujiCommandSettingTypeAddress>(ProtocolMessages.NotSupportedDataType);
                    addType = match.Value;
                    addOffset = address.Substring(addType.Length);
                }
                else
                {
                    string[] splits = address.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                    if (splits[0][0] != 'W')
                        return new OperationResult<FujiCommandSettingTypeAddress>(ProtocolMessages.NotSupportedDataType);
                    addType = splits[0];
                    addOffset = splits[1];
                }

                fujiAddress.AddressHeader = addType;
                fujiAddress.AddressStart = Convert.ToInt32(addOffset);
                fujiAddress.Length = length;
                if (addType == "TS")
                    fujiAddress.DataCode = 0x0A;
                else if (addType == "TR")
                    fujiAddress.DataCode = 0x0B;
                else if (addType == "CS")
                    fujiAddress.DataCode = 0x0C;
                else if (addType == "CR")
                    fujiAddress.DataCode = 0x0D;
                else if (addType == "BD")
                    fujiAddress.DataCode = 0x0E;
                else if (addType == "WL")
                    fujiAddress.DataCode = 0x14;
                else if (addType == "B")
                    fujiAddress.DataCode = 0x00;
                else if (addType == "M")
                    fujiAddress.DataCode = 0x01;
                else if (addType == "K")
                    fujiAddress.DataCode = 0x02;
                else if (addType == "F")
                    fujiAddress.DataCode = 0x03;
                else if (addType == "A")
                    fujiAddress.DataCode = 0x04;
                else if (addType == "D")
                    fujiAddress.DataCode = 0x05;
                else if (addType == "S")
                    fujiAddress.DataCode = 0x08;
                else if (addType.StartsWith("W"))
                {
                    int add = Convert.ToInt32(addType.Substring(1));
                    if (add == 9)
                        fujiAddress.DataCode = 0x09;
                    else if (add >= 21 && add <= 26)
                        fujiAddress.DataCode = (byte)add;
                    else if (add >= 30 && add <= 109)
                        fujiAddress.DataCode = (byte)add;
                    else if (add >= 120 && add <= 123)
                        fujiAddress.DataCode = (byte)add;
                    else if (add == 125)
                        fujiAddress.DataCode = (byte)add;
                    else
                        return new OperationResult<FujiCommandSettingTypeAddress>(ProtocolMessages.NotSupportedDataType);
                }
                else
                    return new OperationResult<FujiCommandSettingTypeAddress>(ProtocolMessages.NotSupportedDataType);
                //switch (fujiAddress.DataCode)
                //{
                //	case 0x09:
                //	case 0x0A:
                //	case 0x0B:
                //	case 0x0C:
                //	case 0x0D:
                //	case 0x0E:
                //	case 0x19:
                //		{
                //			fujiAddress.Length = (ushort)(length > 1 ? length / 2 : length);
                //			break;
                //		}
                //	case 0x08:
                //		{
                //			fujiAddress.Length = (ushort)(length == 0 ? 1 : length * 2);
                //			break;
                //		}
                //}
                return OperationResult.CreateSuccessResult(fujiAddress);
            }
            catch (Exception ex)
            {
                return new OperationResult<FujiCommandSettingTypeAddress>(ex.Message);
            }
        }
    }
}
