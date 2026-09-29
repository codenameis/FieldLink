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

    /// <summary>MelsecQnA3EAsciiFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class MelsecQnA3EAsciiFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 18;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            byte[] buffer = new byte[4];
            buffer[0] = header[14];
            buffer[1] = header[15];
            buffer[2] = header[16];
            buffer[3] = header[17];
            return Convert.ToInt32(Encoding.ASCII.GetString(buffer), 16);
        }

        /// <inheritdoc />
        protected override bool ValidateHeader(byte[] header, byte[] request)
        {
            if (header == null)
                return false;
            if (header[0] == (byte)'D' && header[1] == (byte)'0' && header[2] == (byte)'0' && header[3] == (byte)'0')
                return true;
            else
                return false;
        }
    }
}
