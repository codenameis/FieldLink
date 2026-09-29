using FieldLink.PlcDrivers.Modbus;
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

    /// <summary>FujiSpbFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class FujiSpbFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 5;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            if (header == null)
                return 0;
            return Convert.ToInt32(Encoding.ASCII.GetString(header, 3, 2), 16) * 2 + 2;
        }

        /// <inheritdoc />
        protected override bool IsCompleteCore(byte[] send, byte[] received)
        {
            return ModbusFrameRules.CheckAsciiReceiveDataComplete(received);
        }
    }
}
