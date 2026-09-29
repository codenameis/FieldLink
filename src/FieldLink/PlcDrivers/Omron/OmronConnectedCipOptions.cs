using FieldLink.PlcDrivers.AllenBradley;
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
    public sealed class OmronConnectedCipOptions
    {
        /// <summary>Forward Open 요청에 기록하는 연결 시간 초과 배수입니다.</summary>
        public byte ConnectionTimeoutMultiplier { get; set; } = 2;
        /// <summary>호출자가 생성하여 전달하는 4바이트 발신자 시리얼 번호입니다.</summary>
        public byte[] OriginatorSerialNumber { get; set; } = new byte[4];
        /// <summary>연결형 CIP 프레임에 사용할 식별자와 순서 번호입니다.</summary>
        public ConnectedCipOptions Connection { get; set; } = new ConnectedCipOptions();
        /// <summary>프로토콜의 바이트 순서와 값 변환 규칙입니다.</summary>
        public IProtocolValueConverter ValueConverter { get; set; } = new ProtocolValueConverter();
    }
}
