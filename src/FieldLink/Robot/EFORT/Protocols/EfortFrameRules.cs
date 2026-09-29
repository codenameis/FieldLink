using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.EFORT.Protocols
{

    /// <summary>EfortFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class EfortFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 18;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            int length = BitConverter.ToInt16(header, 16) - 18;
            if (length < 0)
                length = 0;
            return length;
        }
    }
}
