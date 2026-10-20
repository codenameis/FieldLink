namespace FieldLink.Plc;

/// <summary>PLC 작업이 실패한 이유의 분류입니다.</summary>
/// <remarks>호출자 실수(잘못된 주소, null, Open 전 호출)는 이 분류가 아니라 예외로 즉시 알립니다.</remarks>
public enum PlcErrorKind
{
    /// <summary>연결 또는 프로토콜 초기 협상(핸드셰이크)에 실패했습니다.</summary>
    ConnectionFailed,

    /// <summary>작업 중 연결이 끊어졌습니다.</summary>
    ConnectionLost,

    /// <summary>제한 시간 안에 응답을 받지 못했습니다.</summary>
    Timeout,

    /// <summary>응답이 손상되었거나 프로토콜 형식과 맞지 않습니다.</summary>
    InvalidResponse,

    /// <summary>장치가 정상 응답으로 요청을 거절했습니다. <see cref="PlcError.DeviceErrorCode"/>를 확인하세요.</summary>
    DeviceError,
}
