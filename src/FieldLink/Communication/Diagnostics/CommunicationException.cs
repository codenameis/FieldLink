using System;
using System.IO;
using System.Net.Sockets;

namespace FieldLink.Communication.Diagnostics
{
    /// <summary>디버깅에 필요한 작업 문맥과 원인 예외를 보존하는 통신 오류입니다.</summary>
    public sealed class CommunicationException : IOException
    {
        internal CommunicationException(CommunicationFailure failure, CommunicationStage stage,
            string remoteAddress, long bytesSent, long bytesReceived, Exception cause)
            : base($"{failure} during {stage} to {remoteAddress}: {cause.Message}", cause)
        {
            Failure = failure;
            Stage = stage;
            RemoteAddress = remoteAddress;
            BytesSent = bytesSent;
            BytesReceived = bytesReceived;
            SocketErrorCode = (cause as SocketException)?.SocketErrorCode;
        }

        /// <summary>분류된 실패 이유입니다.</summary>
        public CommunicationFailure Failure { get; }
        /// <summary>실패 단계입니다.</summary>
        public CommunicationStage Stage { get; }
        /// <summary>작업 대상 주소입니다.</summary>
        public string RemoteAddress { get; }
        /// <summary>소켓 API가 완료를 확인한 송신 바이트 합계입니다. 장치 처리 완료를 의미하지 않습니다.</summary>
        public long BytesSent { get; }
        /// <summary>소켓 API가 완료를 확인한 수신 바이트 합계입니다.</summary>
        public long BytesReceived { get; }
        /// <summary>원인 예외가 소켓 오류이면 해당 운영체제 오류 코드입니다.</summary>
        public SocketError? SocketErrorCode { get; }
    }
}
