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

    /// <summary>MelsecFxLinksFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class MelsecFxLinksFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public MelsecFxLinksFrameRules(int format, bool sumCheck)
        {
            if (format != 1 && format != 4)
                throw new ArgumentOutOfRangeException(nameof(format));
            this.format = format;
            this.sumCheck = sumCheck;
        }

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
            byte[] data = received;
            if (data.Length < 5)
                return false;
            if (format == 1)
            {
                if (data[0] == AsciiControl.NAK)
                    return data.Length == 7;
                if (data[0] == AsciiControl.ACK)
                    return data.Length == 5;
                if (data[0] == AsciiControl.STX)
                {
                    if (sumCheck)
                        return data[data.Length - 3] == AsciiControl.ETX;
                    else
                        return data[data.Length - 1] == AsciiControl.ETX;
                }

                return false;
            }
            else if (format == 4)
            {
                return data[data.Length - 1] == AsciiControl.LF && data[data.Length - 2] == AsciiControl.CR;
            }

            return false;
        }

        private readonly int format;

        private readonly bool sumCheck;
    }
}
