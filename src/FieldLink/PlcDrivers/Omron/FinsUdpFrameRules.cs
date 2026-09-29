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

    /// <summary>FinsUdpFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class FinsUdpFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 0;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            return 0;
        }

        /// <inheritdoc />
        protected override bool IsCompleteCore(byte[] request, byte[] received) => received.Length >= 10;

        /// <inheritdoc />
        protected override ResponseDisposition ClassifyResponseCore(byte[] send, byte[] receive)
        {
            if (send.Length < 10 || receive.Length < 10)
                return ResponseDisposition.Reject;
            if (send.Length >= 10 && receive.Length >= 10)
            {
                // SID가 일치하지 않으면 데이터를 계속 수신합니다.
                if (send[9] == receive[9])
                    return ResponseDisposition.Accept;
                return ResponseDisposition.Ignore;
            }

            return base.ClassifyResponseCore(send, receive);
        }
    }
}
