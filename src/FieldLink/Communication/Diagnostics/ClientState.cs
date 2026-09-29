namespace FieldLink.Communication.Diagnostics
{
    /// <summary>소켓의 로컬 수명 상태입니다. 장치 생존 상태와 구분합니다.</summary>
    public enum ClientState
    {
        /// <summary>소켓이 닫혀 있습니다.</summary>
        Closed,
        /// <summary>연결 또는 바인딩 중입니다.</summary>
        Opening,
        /// <summary>송수신할 수 있도록 소켓을 준비했습니다.</summary>
        Open,
        /// <summary>작업 실패로 소켓을 닫았습니다. 다시 열어야 합니다.</summary>
        Faulted,
        /// <summary>전송 객체가 해제되어 재사용할 수 없습니다.</summary>
        Disposed
    }
}
