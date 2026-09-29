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
    public sealed class McFrameOptions
    {
        /// <summary>대상 네트워크 번호입니다. 기본값은 0입니다.</summary>
        /// <remarks>연결할 PLC의 네트워크 구성에 맞춰 지정합니다.</remarks>
        public byte NetworkNumber { get; set; }
        /// <summary>대상 PLC 번호입니다. 로컬 국번은 0xFF입니다.</summary>
        public byte PLCNumber { get; set; } = 0xff;
        /// <summary>대상 모듈의 I/O 번호입니다. 제어 CPU의 기본값은 0x03FF입니다.</summary>
        public ushort TargetIOStation { get; set; } = 0x03ff;
        /// <summary>대상 네트워크의 국번입니다. 기본값은 0입니다.</summary>
        /// <remarks>연결할 PLC의 네트워크 구성에 맞춰 지정합니다.</remarks>
        public byte NetworkStationNumber { get; set; }
    }
}
