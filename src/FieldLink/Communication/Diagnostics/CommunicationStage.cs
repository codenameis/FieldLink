namespace FieldLink.Communication.Diagnostics
{
    /// <summary>실패가 발생한 작업 단계입니다.</summary>
    public enum CommunicationStage
    {
        /// <summary>앞선 작업의 완료를 기다리는 중입니다.</summary>
        Waiting,
        /// <summary>소켓을 연결하거나 바인딩하는 중입니다.</summary>
        Connecting,
        /// <summary>송신 중입니다.</summary>
        Sending,
        /// <summary>수신 중입니다.</summary>
        Receiving,
        /// <summary>프레임 길이 또는 응답을 판정하는 중입니다.</summary>
        Validating,
        /// <summary>요청이 없는 동안 TCP 종료 여부를 확인하는 중입니다.</summary>
        Monitoring
    }
}
