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

    /// <summary>SiemensPpiFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class SiemensPpiFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 0;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            return 0;
        }

        /// <inheritdoc />
        protected override bool IsCompleteCore(byte[] send, byte[] received)
        {
            return (received.Length == 1 && received[0] == 0xE5) ||
                (received.Length > 6 && received[0] == 0x68 && received[1] + 6 == received.Length && received[received.Length - 1] == AsciiControl.SYN) ||
                (received.Length > 6 && received[0] == 0x03 && received[3] == received.Length);
        }
    }
}
