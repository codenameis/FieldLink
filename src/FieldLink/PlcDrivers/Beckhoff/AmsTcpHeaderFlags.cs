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

namespace FieldLink.PlcDrivers.Beckhoff
{
    /// <summary>AMS 메시지의 명령 번호</summary>
    public enum AmsTcpHeaderFlags : ushort
    {
        /// <summary>AmsCommand (AMS_TCP_PORT_AMS_CMD, 0x0000)</summary>
        Command = 0x00,
        /// <summary>포트 클로즈 명령 (AMS_TCP_PORT_CLOSE, 0x0001)</summary>
        PortClose = 0x01,
        /// <summary>포트 연결 명령 (AMS_TCP_PORT_CONNECT, 0x1000)</summary>
        PortConnect = 0x1000,
        /// <summary>라우터 통지 (AMS_TCP_PORT_ROUTER_NOTE, 0x1001)</summary>
        RouterNotification = 0x1001,
        /// <summary>LocalNetId 헤더를 가져오기</summary>
        GetLocalNetId = 0x1002
    }
}
