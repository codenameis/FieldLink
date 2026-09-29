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
    /// <summary>프레임 구성과 응답 해석에 필요한 값을 전달합니다. 연결이나 순서 번호 발급은 수행하지 않습니다.</summary>
    public sealed class AdsFrameOptions
    {
        /// <summary>요청과 응답을 대응시키기 위해 호출자가 지정하는 ADS 식별자입니다.</summary>
        public uint InvokeId { get; set; }
        /// <summary>대상 AMS Net ID 6바이트와 AMS 포트 2바이트입니다.</summary>
        public byte[] targetAMSNetId { get; set; } = new byte[]
        {
            0,
            0,
            0,
            0,
            1,
            1,
            0x53,
            3
        };
        /// <summary>발신 AMS Net ID 6바이트와 AMS 포트 2바이트입니다.</summary>
        public byte[] sourceAMSNetId { get; set; } = new byte[]
        {
            0,
            0,
            0,
            0,
            1,
            1,
            0,
            0
        };
        /// <summary>프로토콜의 바이트 순서와 값 변환 규칙입니다.</summary>
        public IProtocolValueConverter ValueConverter { get; set; } = new ProtocolValueConverter();
    }
}
