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

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>프레임 구성과 응답 해석에 필요한 값을 전달합니다. 연결이나 순서 번호 발급은 수행하지 않습니다.</summary>
    public sealed class A3CResponseOptions
    {
        /// <summary>대상 장치의 국번입니다.</summary>
        public byte Station { get; set; }
        /// <summary>Format 프로토콜 값입니다.</summary>
        public int Format { get; set; } = 1;
        /// <summary>합계 체크섬을 사용하면 true입니다.</summary>
        public bool SumCheck { get; set; } = true;
    }
}
