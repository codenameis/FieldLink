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

namespace FieldLink.PlcDrivers.Toledo
{
    /// <summary>Toledo 표준 형식의 데이터 클래스 객체</summary>
    public class ToledoStandardData
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public ToledoStandardData()
        {
        }

        /// <summary>버퍼에서 표준 형식의 객체를 로드합니다.</summary>
        /// <param name = "buffer">버퍼</param>
        public ToledoStandardData(byte[] buffer)
        {
            if (buffer[0] == 0x02)
            {
                ParseFromStandardOutput(buffer);
            }
            else if (buffer[0] == 0x01)
            {
                ParseFromExpandOutput(buffer);
            }
        }

        /// <summary>TRUE는 순중량이고, FALSE는 총중량입니다.</summary>
        public bool Suttle { get; set; }
        /// <summary>이 값은 음수입니다.</summary>
        public bool Symbol { get; set; }
        /// <summary>범위를 벗어났는지</summary>
        public bool BeyondScope { get; set; }
        /// <summary>동적인지, TRUE가 동적이고, FALSE는 정적입니다</summary>
        public bool DynamicState { get; set; }
        /// <summary>단위</summary>
        public string Unit { get; set; }
        /// <summary>인쇄 여부</summary>
        public bool IsPrint { get; set; }
        /// <summary>10이 확장되었는지</summary>
        public bool IsTenExtend { get; set; }
        /// <summary>무게</summary>
        public float Weight { get; set; }
        /// <summary>용기 무게</summary>
        public float Tare { get; set; }
        /// <summary>용기 중량 설정 방식입니다. 0: 미설정, 1: 버튼 설정, 2: 사전 설정, 3: 메모리값. 확장 출력에만 적용됩니다.</summary>
        public int TareType { get; set; }
        /// <summary>데이터의 유효성</summary>
        public bool DataValid { get; set; } = true;
        /// <summary>확장된 출력 모드</summary>
        public bool IsExpandOutput { get; set; }

        /// <summary>분석된 데이터의 원본 바이트</summary>
        [Newtonsoft.Json.JsonIgnore]
        public byte[] SourceData { get; set; }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString() => $"ToledoStandardData[{Weight}]";
        /// <summary>연속적인 표준 모형에서 실제 데이터 정보를 분석합니다.</summary>
        /// <param name = "buffer">수신된 데이터 버퍼</param>
        private void ParseFromStandardOutput(byte[] buffer)
        {
            Weight = float.Parse(Encoding.ASCII.GetString(buffer, 4, 6));
            Tare = float.Parse(Encoding.ASCII.GetString(buffer, 10, 6));
            switch (buffer[1] & 0x07)
            {
                case 0:
                    Weight *= 100;
                    Tare *= 100;
                    break;
                case 1:
                    Weight *= 10;
                    Tare *= 10;
                    break;
                case 2:
                    break;
                case 3:
                    Weight /= 10;
                    Tare /= 10;
                    break;
                case 4:
                    Weight /= 100;
                    Tare /= 100;
                    break;
                case 5:
                    Weight /= 1000;
                    Tare /= 1000;
                    break;
                case 6:
                    Weight /= 10000;
                    Tare /= 10000;
                    break;
                case 7:
                    Weight /= 100000;
                    Tare /= 100000;
                    break;
            }

            Suttle = ProtocolBytes.BoolOnByteIndex(buffer[2], 0);
            Symbol = ProtocolBytes.BoolOnByteIndex(buffer[2], 1);
            BeyondScope = ProtocolBytes.BoolOnByteIndex(buffer[2], 2);
            DynamicState = ProtocolBytes.BoolOnByteIndex(buffer[2], 3);
            switch (buffer[3] & 0x07)
            {
                case 0:
                    Unit = ProtocolBytes.BoolOnByteIndex(buffer[2], 4) ? "kg" : "lb";
                    break;
                case 1:
                    Unit = "g";
                    break;
                case 2:
                    Unit = "t";
                    break;
                case 3:
                    Unit = "oz";
                    break;
                case 4:
                    Unit = "ozt";
                    break;
                case 5:
                    Unit = "dwt";
                    break;
                case 6:
                    Unit = "ton";
                    break;
                case 7:
                    Unit = "newton";
                    break;
            }

            IsPrint = ProtocolBytes.BoolOnByteIndex(buffer[3], 3);
            IsTenExtend = ProtocolBytes.BoolOnByteIndex(buffer[3], 4);
            SourceData = buffer;
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "buffer">프레임 데이터가 담긴 버퍼입니다.</param>
        private void ParseFromExpandOutput(byte[] buffer)
        {
            IsExpandOutput = true;
            string strWeight = Encoding.ASCII.GetString(buffer, 6, 9).Replace(" ", "");
            if (!string.IsNullOrEmpty(strWeight))
                Weight = float.Parse(strWeight);
            string strTare = Encoding.ASCII.GetString(buffer, 15, 8).Replace(" ", "");
            if (!string.IsNullOrEmpty(strTare))
                Tare = float.Parse(strTare);
            switch (buffer[2] & 0x0F)
            {
                case 0:
                    Unit = "None";
                    break;
                case 1:
                    Unit = "lb";
                    break;
                case 2:
                    Unit = "kg";
                    break;
                case 3:
                    Unit = "g";
                    break;
                case 4:
                    Unit = "t";
                    break;
                case 5:
                    Unit = "ton";
                    break;
                case 8:
                    Unit = "oz";
                    break;
                case 9:
                    Unit = "newton";
                    break;
            }

            DynamicState = ProtocolBytes.BoolOnByteIndex(buffer[2], 6);
            Suttle = ProtocolBytes.BoolOnByteIndex(buffer[3], 0);
            TareType = (buffer[3] & 0x06) >> 1;
            BeyondScope = ProtocolBytes.BoolOnByteIndex(buffer[4], 1) || ProtocolBytes.BoolOnByteIndex(buffer[4], 2);
            IsPrint = ProtocolBytes.BoolOnByteIndex(buffer[4], 4);
            DataValid = ProtocolBytes.BoolOnByteIndex(buffer[4], 0);
            SourceData = buffer;
        }
    }
}
