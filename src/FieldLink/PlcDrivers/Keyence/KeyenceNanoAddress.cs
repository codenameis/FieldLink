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

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>킨스 상위 링크 프로토콜의 주소 클래스 객체</summary>
    public class KeyenceNanoAddress : DeviceAddress
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public KeyenceNanoAddress()
        {
        }

        /// <summary>지정된 매개 변수를 통해 객체를 인스턴스화합니다.</summary>
        /// <param name = "dataCode">데이터 타입</param>
        /// <param name = "address">오프셋 주소</param>
        /// <param name = "splits">절단 희망 길이</param>
        public KeyenceNanoAddress(string dataCode, int address, int splits)
        {
            this.DataCode = dataCode;
            this.AddressStart = address;
            this.SplitLength = splits;
        }

        /// <summary>데이터를 가져오기 위한 코드, 또는 읽기를 대기하도록 설정</summary>
        public string DataCode { get; set; }
        /// <summary>데이터 길이 정보를 가져오거나 읽기를 설정할 때 절단</summary>
        public int SplitLength { get; set; }

        /// <summary>주소를 얻을 수 있는 문자열 표현 방법</summary>
        /// <returns>문자열 정보</returns>
        public string GetAddressStartFormat()
        {
            switch (this.DataCode)
            {
                case "":
                case "CR":
                case "MR":
                case "LR":
                    return AddressStart >= 16 ? $"{AddressStart / 16}{AddressStart % 16:D2}" : $"{AddressStart % 16}";
                case "B":
                case "VB":
                case "W":
                    return AddressStart.ToString("X");
                default:
                    return AddressStart.ToString();
            }
        }

        /// <summary>정해진 주소 정보에서 실제 장치 주소 정보로 분해</summary>
        /// <param name = "address">주소 정보</param>
        /// <param name = "length">데이터 길이</param>
        public override void Parse(string address, ushort length)
        {
            OperationResult<KeyenceNanoAddress> analysis = ParseFrom(address, length);
            if (!analysis.IsSuccess)
                throw new FormatException(analysis.Message);
            AddressStart = analysis.Content.AddressStart;
            DataCode = analysis.Content.DataCode;
            SplitLength = analysis.Content.SplitLength;
        }

        /// <summary>위치 주소 변환 메서드, 101은 10.1은 10*16+1=161</summary>
        /// <param name = "address">주소 정보</param>
        /// <returns>실제 주소 정보</returns>
        private static int CalculateAddress(string address)
        {
            int add = 0;
            if (address.IndexOf(".") < 0)
            {
                if (address.Length <= 2)
                    add = Convert.ToInt32(address);
                else
                    add = Convert.ToInt32(address.Substring(0, address.Length - 2)) * 16 + Convert.ToInt32(address.Substring(address.Length - 2));
            }
            else
            {
                add = Convert.ToInt32(address.Substring(0, address.IndexOf("."))) * 16;
                string bit = address.Substring(address.IndexOf(".") + 1);
                add += AddressParameters.CalculateBitStartIndex(bit);
            }

            return add;
        }

        /// <summary>키너스 상위 링크 프로토콜의 주소 정보를 분석합니다.</summary>
        /// <param name = "address">문자열 주소</param>
        /// <param name = "length">길이 정보</param>
        /// <returns>성공 주소</returns>
        public static OperationResult<KeyenceNanoAddress> ParseFrom(string address, ushort length)
        {
            try
            {
                if (address.StartsWith("CTH") || address.StartsWith("cth"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("CTH", Convert.ToInt32(address.Substring(3)), 2));
                else if (address.StartsWith("CTC") || address.StartsWith("ctc"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("CTC", Convert.ToInt32(address.Substring(3)), 4));
                else if (address.StartsWith("CR") || address.StartsWith("cr"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("CR", CalculateAddress(address.Substring(2)), 256));
                else if (address.StartsWith("MR") || address.StartsWith("mr"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("MR", CalculateAddress(address.Substring(2)), 256));
                else if (address.StartsWith("LR") || address.StartsWith("lr"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("LR", CalculateAddress(address.Substring(2)), 256));
                else if (address.StartsWith("DM") || address.StartsWith("dm"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("DM", Convert.ToInt32(address.Substring(2)), 256));
                else if (address.StartsWith("CM") || address.StartsWith("cm"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("CM", Convert.ToInt32(address.Substring(2)), 256));
                else if (address.StartsWith("TM") || address.StartsWith("tm"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("TM", Convert.ToInt32(address.Substring(2)), 256));
                else if (address.StartsWith("VM") || address.StartsWith("vm"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("VM", Convert.ToInt32(address.Substring(2)), 256));
                else if (address.StartsWith("VB") || address.StartsWith("vb"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("VB", Convert.ToInt32(address.Substring(2), 16), 256));
                else if (address.StartsWith("EM") || address.StartsWith("em"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("EM", Convert.ToInt32(address.Substring(2)), 256));
                else if (address.StartsWith("FM") || address.StartsWith("fm"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("FM", Convert.ToInt32(address.Substring(2)), 256));
                else if (address.StartsWith("ZF") || address.StartsWith("zf"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("ZF", Convert.ToInt32(address.Substring(2)), 256));
                else if (address.StartsWith("AT") || address.StartsWith("at"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("AT", Convert.ToInt32(address.Substring(2)), 8));
                else if (address.StartsWith("TS") || address.StartsWith("ts"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("TS", Convert.ToInt32(address.Substring(2)), 64));
                else if (address.StartsWith("TC") || address.StartsWith("tc"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("TC", Convert.ToInt32(address.Substring(2)), 64));
                else if (address.StartsWith("CC") || address.StartsWith("cc"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("CC", Convert.ToInt32(address.Substring(2)), 64));
                else if (address.StartsWith("CS") || address.StartsWith("cs"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("CS", Convert.ToInt32(address.Substring(2)), 64));
                else if (address.StartsWith("W") || address.StartsWith("w"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("W", Convert.ToInt32(address.Substring(1), 16), 256));
                else if (address.StartsWith("Z") || address.StartsWith("z"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("Z", Convert.ToInt32(address.Substring(1)), 12));
                else if (address.StartsWith("R") || address.StartsWith("r"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("", CalculateAddress(address.Substring(1)), 256));
                else if (address.StartsWith("B") || address.StartsWith("b"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("B", Convert.ToInt32(address.Substring(1), 16), 256));
                else if (address.StartsWith("T") || address.StartsWith("t"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("T", Convert.ToInt32(address.Substring(1)), 64));
                else if (address.StartsWith("C") || address.StartsWith("c"))
                    return OperationResult.CreateSuccessResult(new KeyenceNanoAddress("C", Convert.ToInt32(address.Substring(1)), 64));
                else
                    throw new Exception(ProtocolMessages.NotSupportedDataType);
            }
            catch (Exception ex)
            {
                return new OperationResult<KeyenceNanoAddress>(ex.Message);
            }
        }
    }
}
