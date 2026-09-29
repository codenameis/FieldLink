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
    public sealed class FinsUdpFrameOptions
    {
        /// <summary>FINS 정보 제어 필드입니다.</summary>
        public byte ICF { get; set; } = 0x80;
        /// <summary>FINS 예약 필드입니다.</summary>
        public byte RSV { get; set; }
        /// <summary>FINS 게이트웨이 통과 수입니다.</summary>
        public byte GCT { get; set; } = 2;
        /// <summary>대상 네트워크 주소입니다.</summary>
        public byte DNA { get; set; }
        /// <summary>대상 노드 주소입니다. FINS/TCP에서는 노드 주소 응답에서 얻은 값을 사용합니다.</summary>
        public byte DA1 { get; set; }
        /// <summary>대상 유닛 주소입니다.</summary>
        public byte DA2 { get; set; }
        /// <summary>발신 네트워크 주소입니다.</summary>
        public byte SNA { get; set; }
        /// <summary>발신 노드 주소입니다. FINS/TCP에서는 노드 주소 응답에서 얻은 값을 사용합니다.</summary>
        public byte SA1 { get; set; } = 13;
        /// <summary>발신 유닛 주소입니다.</summary>
        public byte SA2 { get; set; }
        /// <summary>호출자가 지정한 메시지 순서 번호입니다.</summary>
        public byte SID { get; set; }
        /// <summary>프레임 시작을 나타내는 FINS 시그니처 바이트입니다.</summary>
        public byte[] handSingle { get; set; } = new byte[]
        {
            0x46,
            0x49,
            0x4e,
            0x53
        };
    }
}
