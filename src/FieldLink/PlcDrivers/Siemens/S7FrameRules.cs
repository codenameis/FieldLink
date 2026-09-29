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

    /// <summary>S7FrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class S7FrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 4;

        /// <inheritdoc />
        protected override bool ValidateHeader(byte[] header, byte[] request)
        {
            if (header == null)
                return false;
            if (header[0] == 0x03 && header[1] == 0x00)
                return true;
            else
                return false;
        }

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            if (header?.Length >= 4)
            {
                int length = header[2] * 256 + header[3] - 4;
                if (length < 0)
                    length = 0;
                return length;
            }
            else
                return 0;
        }
    }
}
