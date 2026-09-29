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

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>프레임 구성과 응답 해석에 필요한 값을 전달합니다. 연결이나 순서 번호 발급은 수행하지 않습니다.</summary>
    public sealed class S7PlusCodecOptions
    {
        /// <summary>프레임에 기록할 세션 식별자입니다.</summary>
        public uint SessionID { get; set; }
        /// <summary>호출자가 지정한 메시지 순서 번호입니다.</summary>
        public int SequenceNumber { get; set; }
        /// <summary>S7+ 요청의 무결성 식별자입니다.</summary>
        public uint IntegrityId { get; set; }
        /// <summary>ISO 연결 요청의 로컬 TSAP입니다. 기본값은 0x0600입니다.</summary>
        public ushort LocalTSAP { get; set; } = 0x0600;
        /// <summary>ISO 연결 요청의 대상 TSAP 바이트입니다.</summary>
        public byte[] destTSAP { get; set; } = Encoding.ASCII.GetBytes("SIMATIC-ROOT-HMI");
        /// <summary>프로토콜의 바이트 순서와 값 변환 규칙입니다.</summary>
        public IProtocolValueConverter ValueConverter { get; set; } = new ProtocolValueConverter(ByteOrder.BigEndian);
        /// <summary>S7+ ISO 연결 요청의 기본 헤더입니다.</summary>
        public byte[] iso_head { get; set; } = new byte[]
        {
            3,
            0,
            0,
            0x24,
            0x1f,
            0xe0,
            0,
            0,
            0,
            1,
            0,
            0xc0,
            1,
            0x0a,
            0xc1,
            2,
            1,
            0,
            0xc2,
            0x10
        };
    }
}
