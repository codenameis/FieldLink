namespace FieldLink.Robot;

/// <summary>로봇 통신 작업이 실패한 원인의 분류입니다.</summary>
public enum RobotErrorKind
{
    /// <summary>연결 수립 또는 초기 세션 협상에 실패했습니다.</summary>
    ConnectionFailed,

    /// <summary>통신 도중 연결이 끊어졌거나 전송 입출력 오류가 발생했습니다.</summary>
    ConnectionLost,

    /// <summary>제한 시간 내에 응답을 받지 못했습니다.</summary>
    Timeout,

    /// <summary>수신 프레임이 손상되었거나 프로토콜 규격과 일치하지 않습니다.</summary>
    InvalidResponse,

    /// <summary>로봇 컨트롤러가 정상 프로토콜 오류 응답으로 요청을 거절했습니다.</summary>
    DeviceError,

    /// <summary>HTTP 인증 또는 세션 인증에 실패했습니다.</summary>
    AuthenticationFailed,

    /// <summary>응답 데이터 본문 또는 주소·구문 해석에 실패했습니다.</summary>
    ParseError,

    /// <summary>해당 로봇 프로토콜에서 지원하지 않는 연산입니다.</summary>
    Unsupported,
}
