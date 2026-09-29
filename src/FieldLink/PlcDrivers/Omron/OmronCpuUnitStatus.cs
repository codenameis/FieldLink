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

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>옴롱 CPU 상태 정보</summary>
    public class OmronCpuUnitStatus
    {
        /// <summary>기본 객체를 생성합니다.</summary>
        public OmronCpuUnitStatus()
        {
        }

        /// <summary>원본의 바이트 배열에서 인스턴스</summary>
        /// <param name = "data">원시 바이트 데이터</param>
        public OmronCpuUnitStatus(byte[] data)
        {
            Status = data[0].GetBoolByIndex(0) ? "Run" : "Stop";
            BatteryStatus = data[0].GetBoolByIndex(2) ? "Present" : "No";
            CpuStatus = data[0].GetBoolByIndex(7) ? "Standby" : "Normal";
            Mode = data[1] == 0x00 ? "PROGRAM" : data[1] == 0x02 ? "MONITOR" : data[1] == 0x04 ? "RUN" : "";
            ErrorCode = data[8] * 256 + data[9];
            if (ErrorCode > 0)
                ErrorMessage = Encoding.ASCII.GetString(data, 10, 16).TrimEnd(new char[] { ' ', '\0' });
        }

        /// <summary>실행 또는 중지</summary>
        public string Status { get; set; }
        /// <summary>No 또는 Present</summary>
        public string BatteryStatus { get; set; }
        /// <summary>정상 또는 대기</summary>
        public string CpuStatus { get; set; }
        /// <summary>PROGRAM, MONITOR, RUN</summary>
        public string Mode { get; set; }
        /// <summary>명령이 실행 될 때 발생하는 오류 중, 오류 코드는 가장 심각한 것을 나타냅니다. 오류가 없으면 0000 (hex)</summary>
        public int ErrorCode { get; set; }
        /// <summary>FAL ((006) 또는 FALS ((007) 의 실행에서 메시지를 나타냅니다. 오류 메시지가 없다면, 
        /// 또는 FAL ((006) 또는 FALS ((007) 가 실행되지 않으면 16개의 스페이스 (((ASCII 20) 가 반환됩니다.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString() => $"OmronCpuUnitStatus[{Status}]";
    }
}
