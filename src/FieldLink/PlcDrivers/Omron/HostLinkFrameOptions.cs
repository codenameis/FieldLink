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
    /// <summary>프레임 구성과 응답 해석에 필요한 값을 전달합니다. 연결이나 순서 번호 발급은 수행하지 않습니다.</summary>
    public sealed class HostLinkFrameOptions
    {
        /// <summary>FINS 정보 제어 필드입니다.</summary>
        public byte ICF { get; set; }
        /// <summary>대상 유닛 주소입니다.</summary>
        public byte DA2 { get; set; }
        /// <summary>발신 유닛 주소입니다.</summary>
        public byte SA2 { get; set; }
        /// <summary>호출자가 지정한 메시지 순서 번호입니다.</summary>
        public byte SID { get; set; }
        /// <summary>Host Link 응답 대기 시간을 나타내는 ASCII 코드입니다. 기본값 0x30은 문자 0입니다.</summary>
        public byte ResponseWaitTime { get; set; } = 0x30;
        /// <summary>Host Link 대상 유닛 번호입니다.</summary>
        public byte UnitNumber { get; set; }
        /// <summary>한 번의 읽기 명령에 포함할 최대 워드 수입니다.</summary>
        public int ReadSplits { get; set; } = 260;
        /// <summary>명령 형식을 결정하는 PLC 기종입니다.</summary>
        public OmronPlcType PlcType { get; set; } = OmronPlcType.CSCJ;
    }
}
