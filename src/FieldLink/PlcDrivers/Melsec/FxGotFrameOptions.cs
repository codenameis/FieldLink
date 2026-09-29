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
    public sealed class FxGotFrameOptions
    {
        /// <summary>GOT 프레임으로 감싸면 true입니다.</summary>
        public bool useGot { get; set; } = false;
        /// <summary>Port 프로토콜 값입니다.</summary>
        public int Port { get; set; }
        /// <summary>IpAddress 프로토콜 값입니다.</summary>
        public string IpAddress { get; set; }
        /// <summary>호출자가 지정하는 GOT 메시지 번호입니다.</summary>
        public int MessageId { get; set; }
    }
}
