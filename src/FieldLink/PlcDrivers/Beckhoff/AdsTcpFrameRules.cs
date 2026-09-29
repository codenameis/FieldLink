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

    /// <summary>AdsTcpFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class AdsTcpFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 6;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            if (header?.Length >= 6)
            {
                
                uint length = (uint)header[2] | ((uint)header[3] << 8) |
                    ((uint)header[4] << 16) | ((uint)header[5] << 24);
                if (length > int.MaxValue - HeaderLength)
                    throw new InvalidDataException("ADS 전체 프레임 길이가 정수 범위를 초과합니다.");
                return (int)length;
            }
            else
                return 0;
        }
    }
}
