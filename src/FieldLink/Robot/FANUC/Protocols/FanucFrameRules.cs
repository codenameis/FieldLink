using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.FANUC.Protocols
{

    /// <summary>FanucFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class FanucFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 56;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            return BitConverter.ToUInt16(header, 4);
        }
    }
}
