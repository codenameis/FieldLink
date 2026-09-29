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

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>프레임 구성과 응답 해석에 필요한 값을 전달합니다. 연결이나 순서 번호 발급은 수행하지 않습니다.</summary>
    public sealed class FastEnetFrameOptions
    {
        /// <summary>LS 요청 헤더의 회사 식별 문자열입니다.</summary>
        public string CompanyID { get; set; } = "LSIS-XGT";
        /// <summary>LS 요청 헤더에 기록할 CPU 기종입니다.</summary>
        public LSCpuInfo cpuInfo { get; set; } = LSCpuInfo.XGK;
        /// <summary>LS PLC의 베이스 번호입니다.</summary>
        public byte baseNo { get; set; }
        /// <summary>LS PLC의 슬롯 번호입니다.</summary>
        public byte slotNo { get; set; } = 3;
        /// <summary>응답에 표시된 CPU 기종입니다.</summary>
        public string CpuType { get; set; }
        /// <summary>응답에 표시된 CPU 오류 여부입니다.</summary>
        public bool CpuError { get; set; }
        /// <summary>응답을 해석하여 얻은 LS CPU 운전 상태입니다.</summary>
        public LSCpuStatus LSCpuStatus { get; set; }
    }
}
