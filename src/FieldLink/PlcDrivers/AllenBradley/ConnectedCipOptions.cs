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
    public sealed class ConnectedCipOptions
    {
        /// <summary>프레임에 기록할 세션 식별자입니다.</summary>
        public uint SessionHandle { get; set; }
        /// <summary>호출자가 제공하는 O→T 연결 식별자입니다.</summary>
        public uint OTConnectionId { get; set; }
        /// <summary>호출자가 지정한 메시지 순서 번호입니다.</summary>
        public ushort SequenceNumber { get; set; }
        /// <summary>프로토콜의 바이트 순서와 값 변환 규칙입니다.</summary>
        public IProtocolValueConverter ValueConverter { get; set; } = new ProtocolValueConverter();
    }
}
