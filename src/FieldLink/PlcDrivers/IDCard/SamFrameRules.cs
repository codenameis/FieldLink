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

namespace FieldLink.PlcDrivers.IDCard
{

    /// <summary>SamFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class SamFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 7;

        /// <inheritdoc />
        protected override bool ValidateHeader(byte[] header, byte[] request)
        {
            if (header == null)
                return true;
            return header[0] == 0xAA && header[1] == 0xAA && header[2] == 0xAA && header[3] == 0x96 && header[4] == 0x69;
        }

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            if (header?.Length >= 7)
                return header[5] * 256 + header[6];
            else
                return 0;
        }
    }
}
