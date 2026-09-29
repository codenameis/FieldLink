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

    /// <summary>MelsecQnA3EBinaryFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class MelsecQnA3EBinaryFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 9;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            return BitConverter.ToUInt16(header, 7);
        }

        /// <inheritdoc />
        protected override bool ValidateHeader(byte[] header, byte[] request)
        {
            if (header == null)
                return false;
            if (header[0] == 0xD0 && header[1] == 0x00)
                return true;
            else
                return false;
        }
    }
}
