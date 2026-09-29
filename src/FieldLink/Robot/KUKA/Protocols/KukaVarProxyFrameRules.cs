using FieldLink.PlcDrivers.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FieldLink.Robot.KUKA.Protocols
{

    /// <summary>KukaVarProxyFrameRules의 프레임 길이·형식·응답 식별 규칙입니다. 요청별 버퍼를 보관하지 않습니다.</summary>
    public class KukaVarProxyFrameRules : ProtocolFrameRules
    {

        /// <inheritdoc />
        public override int HeaderLength => 4;

        /// <inheritdoc />
        protected override int ReadBodyLength(byte[] header, byte[] request)
        {
            if (header?.Length >= 4)
                return header[2] * 256 + header[3];
            else
                return 0;
        }

        /// <inheritdoc />
        protected override int? ReadSequenceId(byte[] header)
        {
            if (header?.Length >= 4)
                return header[0] * 256 + header[1];
            else
                return 0;
        }
    }
}
