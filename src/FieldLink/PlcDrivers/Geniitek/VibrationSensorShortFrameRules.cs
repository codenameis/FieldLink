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

namespace FieldLink.PlcDrivers.Geniitek
{

    /// <summary>VibrationSensorShortFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class VibrationSensorShortFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 9;

        /// <inheritdoc />
        protected override bool ValidateHeader(byte[] header, byte[] request)
        {
            if (header == null)
                return false;
            if (header[0] == 0xAA)
                return true;
            else
                return false;
        }

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            return 0;
        }
    }
}
