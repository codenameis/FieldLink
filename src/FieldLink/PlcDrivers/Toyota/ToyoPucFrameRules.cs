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

namespace FieldLink.PlcDrivers.Toyota
{

    /// <summary>ToyoPucFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class ToyoPucFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 4;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            try
            {
                if (header?.Length >= 4)
                {
                    return BitConverter.ToUInt16(header, 2);
                }
                else
                    return 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
