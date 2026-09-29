using System;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Robot.YASKAWA.Protocols
{
    /// <summary>YRC 고속 이더넷 요청의 고정 부분과 응답 변환입니다. 요청 ID와 송수신은 호출자가 관리합니다.</summary>
    public sealed class YrcEthernetRequest<T>
    {
        private readonly byte[] packet;
        private readonly Func<byte[], OperationResult<T>> parser;
        internal YrcEthernetRequest(byte[] packet, Func<byte[], OperationResult<T>> parser)
        {
            this.packet = packet;
            this.parser = parser;
        }

        /// <summary>요청 ID를 넣은 독립 패킷을 반환합니다. 반환 배열은 호출자가 소유합니다.</summary>
        public byte[] Build(byte requestId)
        {
            var result = (byte[])packet.Clone();
            result[11] = requestId;
            return result;
        }

        /// <summary>완전한 응답을 해석합니다. 원본처럼 식별자 대응 및 YERC/길이 검증은 포함하지 않습니다.</summary>
        public OperationResult<T> ParseResponse(byte[] response) => parser(response);
        internal YrcEthernetRequest<TResult> Then<TResult>(Func<OperationResult<T>, OperationResult<TResult>> convert) => new YrcEthernetRequest<TResult>(packet, response => convert(parser(response)));
    }
}
