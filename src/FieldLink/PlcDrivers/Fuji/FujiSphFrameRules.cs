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

namespace FieldLink.PlcDrivers.Fuji
{

    /// <summary>FujiSphFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class FujiSphFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 20;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            if (header == null)
                return 0;
            return BitConverter.ToUInt16(header, 18);
        }
    }
}
