namespace FieldLink.Communication.Diagnostics
{
    /// <summary>통신 작업이 실패한 이유입니다.</summary>
    public enum CommunicationFailure
    {
        /// <summary>작업 전체의 제한 시간을 초과했습니다.</summary>
        Timeout,
        /// <summary>호출자가 작업을 취소했습니다.</summary>
        Cancelled,
        /// <summary>연결이 종료되었거나 원격 장치가 연결을 끊었습니다.</summary>
        ConnectionClosed,
        /// <summary>운영체제 소켓 오류입니다.</summary>
        SocketError,
        /// <summary>프레임 규칙에 맞지 않는 데이터입니다.</summary>
        InvalidFrame,
        /// <summary>허용한 메시지 크기를 초과했습니다.</summary>
        MessageTooLarge,
        /// <summary>응답 판정기가 응답을 거부했습니다.</summary>
        ResponseRejected,
        /// <summary>통신 객체가 해제되었습니다.</summary>
        Disposed,
        /// <summary>운영체제 시리얼 포트의 입출력 또는 접근 오류입니다.</summary>
        PortError
    }
}
