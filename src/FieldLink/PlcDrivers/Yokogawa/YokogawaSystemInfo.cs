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
    /// <summary>요카이 PLC의 시스템 기본 정보</summary>
    public class YokogawaSystemInfo
    {
        /// <summary>F3SP21-ON과 같은 현재 시스템 ID 이름</summary>
        public string SystemID { get; set; }
        /// <summary>현재 시스템의 수정 버전 번호</summary>
        public string Revision { get; set; }
        /// <summary>현재 시스템 유형은 <b>Sequence</b> 그리고 <b>BASIC</b></summary>
        public string CpuType { get; set; }
        /// <summary>현재 시스템의 프로그램 크기는 Sequence 시스템에서는 전체 단계이고 BASIC 시스템에서는 바이트입니다.</summary>
        public int ProgramAreaSize { get; set; }

        /// <summary>현재 프로토콜 값의 문자열 표현을 반환합니다.</summary>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public override string ToString() => $"YokogawaSystemInfo[{SystemID}]";
        /// <summary>원본 데이터 정보에 따라 파싱<c>YokogawaSystemInfo</c>객체</summary>
        /// <param name = "content">원시 데이터 정보</param>
        /// <returns>결과 객체 분석 성공 여부</returns>
        public static OperationResult<YokogawaSystemInfo> Parse(byte[] content)
        {
            try
            {
                YokogawaSystemInfo systemInfo = new YokogawaSystemInfo();
                systemInfo.SystemID = Encoding.ASCII.GetString(content, 0, 16).Trim('\0', ' ');
                systemInfo.Revision = Encoding.ASCII.GetString(content, 16, 8).Trim('\0', ' ');
                if (content[25] == 0x01 || content[25] == 0x11)
                    systemInfo.CpuType = "Sequence";
                else if (content[25] == 0x02 || content[25] == 0x12)
                    systemInfo.CpuType = "BASIC";
                else
                    systemInfo.CpuType = ProtocolMessages.UnknownError;
                systemInfo.ProgramAreaSize = content[26] * 256 + content[27];
                return OperationResult.CreateSuccessResult(systemInfo);
            }
            catch (Exception ex)
            {
                return new OperationResult<YokogawaSystemInfo>("Parse YokogawaSystemInfo failed: " + ex.Message + Environment.NewLine + "Source: " + content.ToHexString(' '));
            }
        }
    }
}
