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

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>프레임 구성과 응답 해석에 필요한 값을 전달합니다. 연결이나 순서 번호 발급은 수행하지 않습니다.</summary>
    public sealed class CipCommandOptions
    {
        /// <summary>Encapsulation 명령 코드입니다.</summary>
        public ushort CipCommand { get; set; } = 0x6f;
        /// <summary>대상 CPU 슬롯 번호입니다.</summary>
        public byte Slot { get; set; }
        /// <summary>CIP 라우팅 경로 바이트입니다.</summary>
        public byte[] PortSlot { get; set; }
        /// <summary>MessageRouter 프로토콜 값입니다.</summary>
        public MessageRouter MessageRouter { get; set; }
        /// <summary>프레임에 기록할 세션 식별자입니다.</summary>
        public uint SessionHandle { get; set; }
        /// <summary>Encapsulation 요청의 발신자 컨텍스트입니다.</summary>
        public byte[] SenderContext { get; set; }
    }
}
