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

namespace FieldLink.PlcDrivers.XINJE
{
    /// <summary>Xinje 내부 프로토콜의 주소형 객체</summary>
    public class XinjeAddress : DeviceAddress
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public XinjeAddress()
        {
        }

        /// <summary>객체를 인스턴스화하기 위해 유형, 주소 지각, 임의 주소 지정</summary>
        /// <param name = "dataCode">데이터 타입 코드</param>
        /// <param name = "address">이동 주소 정보</param>
        /// <param name = "criticalAddress">임상 주소 정보</param>
        /// <param name = "station">국번 정보</param>
        public XinjeAddress(byte dataCode, int address, int criticalAddress, byte station)
        {
            this.DataCode = dataCode;
            this.AddressStart = address;
            this.CriticalAddress = criticalAddress;
            this.Station = station;
        }

        /// <summary>데이터를 가져오기 위한 코드, 또는 읽기를 대기하도록 설정</summary>
        public byte DataCode { get; set; }
        /// <summary>대상 장치의 국번입니다.</summary>
        public byte Station { get; set; }
        /// <summary>프로토콜 업그레이드를 받을 때 또는 설정할 때 중요한 주소 정보를 얻습니다.</summary>
        public int CriticalAddress { get; set; }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString()
        {
            return AddressStart.ToString();
        }

        /// <summary>실제 Xinje PLC의 주소에서 주소 객체를 파싱</summary>
        /// <param name = "address">XinJE 주소 데이터 정보</param>
        /// <param name = "length">읽을 길이 정보</param>
        /// <param name = "defaultStation">기본 국번 정보</param>
        /// <returns>성공 여부를 나타내는 결과 객체</returns>
        public static OperationResult<XinjeAddress> ParseFrom(string address, ushort length, byte defaultStation)
        {
            OperationResult<XinjeAddress> analysis = ParseFrom(address, defaultStation);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<XinjeAddress>(analysis);
            analysis.Content.Length = length;
            return OperationResult.CreateSuccessResult(analysis.Content);
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "defaultStation">defaultStation에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<XinjeAddress> ParseFrom(string address, byte defaultStation)
        {
            try
            {
                byte stat = (byte)AddressParameters.ExtractParameter(ref address, "s", defaultStation);
                if (address.StartsWith("HSCD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x8B, int.Parse(address.Substring(4)), int.MaxValue, stat));
                else if (address.StartsWith("ETD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x85, int.Parse(address.Substring(3)), 0, stat));
                else if (address.StartsWith("HSD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x8C, int.Parse(address.Substring(3)), 1024, stat));
                else if (address.StartsWith("HTD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x89, int.Parse(address.Substring(3)), 1024, stat));
                else if (address.StartsWith("HCD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x8A, int.Parse(address.Substring(3)), 1024, stat));
                else if (address.StartsWith("SFD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x8E, int.Parse(address.Substring(3)), 4096, stat));
                else if (address.StartsWith("HSC"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x0C, int.Parse(address.Substring(3)), int.MaxValue, stat));
                else if (address.StartsWith("SD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x83, int.Parse(address.Substring(2)), 4096, stat));
                else if (address.StartsWith("TD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x81, int.Parse(address.Substring(2)), 4096, stat));
                else if (address.StartsWith("CD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x82, int.Parse(address.Substring(2)), 4096, stat));
                else if (address.StartsWith("HD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x88, int.Parse(address.Substring(2)), 6144, stat));
                else if (address.StartsWith("FD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x8D, int.Parse(address.Substring(2)), 8192, stat));
                else if (address.StartsWith("ID"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x86, int.Parse(address.Substring(2)), 0, stat));
                else if (address.StartsWith("QD"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x87, int.Parse(address.Substring(2)), 0, stat));
                else if (address.StartsWith("SM"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x0D, int.Parse(address.Substring(2)), 4096, stat));
                else if (address.StartsWith("ET"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x07, int.Parse(address.Substring(2)), 0, stat));
                else if (address.StartsWith("HM"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x08, int.Parse(address.Substring(2)), 6144, stat));
                else if (address.StartsWith("HS"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x09, int.Parse(address.Substring(2)), int.MaxValue, stat));
                else if (address.StartsWith("HT"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x0A, int.Parse(address.Substring(2)), 1024, stat));
                else if (address.StartsWith("HC"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x0B, int.Parse(address.Substring(2)), 1024, stat));
                else if (address.StartsWith("D"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x80, int.Parse(address.Substring(1)), 20480, stat));
                else if (address.StartsWith("M"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x03, int.Parse(address.Substring(1)), 20480, stat));
                else if (address.StartsWith("T"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x05, int.Parse(address.Substring(1)), 4096, stat));
                else if (address.StartsWith("C"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x06, int.Parse(address.Substring(1)), 4096, stat));
                else if (address.StartsWith("Y"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x02, Convert.ToInt32(address.Substring(1), 8), int.MaxValue, stat));
                else if (address.StartsWith("X"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x01, Convert.ToInt32(address.Substring(1), 8), int.MaxValue, stat));
                else if (address.StartsWith("S"))
                    return OperationResult.CreateSuccessResult(new XinjeAddress(0x04, int.Parse(address.Substring(1)), 8000, stat));
                else
                    return new OperationResult<XinjeAddress>(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<XinjeAddress>(ProtocolMessages.AddressFormatWrong + ex.Message);
            }
        }
    }
}
