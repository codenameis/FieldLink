using FieldLink.Communication;
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

namespace FieldLink.PlcDrivers.Omron
{

    /// <summary>FinsTcpFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class FinsTcpFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 16;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            
            if (header == null || header.Length < 8)
                throw new InvalidDataException("FINS/TCP 길이 필드가 없습니다.");
            uint length = ((uint)header[4] << 24) | ((uint)header[5] << 16) |
                ((uint)header[6] << 8) | header[7];
            if (length < 8 || length > int.MaxValue - 8)
                throw new InvalidDataException("FINS/TCP 선언 길이가 올바르지 않습니다.");
            return (int)length - 8;
        }

        /// <inheritdoc />
        protected override bool ValidateHeader(byte[] header, byte[] request)
        {
            if (header == null)
                return true;
            if (header[0] == 0x46 && header[1] == 0x49 && header[2] == 0x4E && header[3] == 0x53)
                return true;
            else
                return false;
        }

        /// <inheritdoc />
        public override int FindHeaderOffset(byte[] headByte)
        {
            if (headByte == null)
                throw new ArgumentNullException(nameof(headByte));
            for (int i = 0; i < headByte.Length - 3; i++)
            {
                if (headByte[i + 0] == 0x46 && headByte[i + 1] == 0x49 && headByte[i + 2] == 0x4E && headByte[i + 3] == 0x53)
                    return i;
            }

            return base.FindHeaderOffset(headByte);
        }

        /// <inheritdoc />
        protected override ResponseDisposition ClassifyResponseCore(byte[] send, byte[] receive)
        {
            if (send == null || receive == null)
                return ResponseDisposition.Accept;
            if (send.Length > 25 && receive.Length > 25)
            {
                // SID가 일치하지 않으면 데이터를 계속 수신합니다.
                if (send[25] == receive[25])
                    return ResponseDisposition.Accept;
                return ResponseDisposition.Ignore;
            }

            return base.ClassifyResponseCore(send, receive);
        }
    }
}
