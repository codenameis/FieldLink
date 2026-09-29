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

    /// <summary>FetchWriteFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class FetchWriteFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 16;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            if (header[5] == 0x05 || header[5] == 0x04)
                return 0;
            if (header[5] == 0x06)
            {
                RequireRequest(request, 16);
                if (header[8] != 0x00)
                    return 0; // 오류가 발생했습니다.
                if (request[8] == 0x01 || request[8] == 0x06 || request[8] == 0x07)
                    return (request[12] * 256 + request[13]) * 2;
                return request[12] * 256 + request[13];
            }
            else if (header[5] == 0x03)
            {
                if (header[8] == 0x01 || header[8] == 0x06 || header[8] == 0x07)
                    return (header[12] * 256 + header[13]) * 2;
                return header[12] * 256 + header[13];
            }

            return 0;
        }

        /// <inheritdoc />
        protected override bool ValidateHeader(byte[] header, byte[] request)
        {
            if (header == null)
                return false;
            if (header[0] == 0x53 && header[1] == 0x35)
                return true;
            else
                return false;
        }

        /// <inheritdoc />
        protected override int? ReadSequenceId(byte[] header)
        {
            return header[3];
        }
    }
}
