namespace FieldLink.Robot.Protocols.Efort.ER7BC10;

/// <summary>EFORT ER7B-C10 프로토콜 패킷의 메모리 정렬 버전입니다.</summary>
public enum EfortProtocolVersion
{
    /// <summary>현행 4바이트 정렬 버전입니다. (요청 38바이트 / 응답 788바이트)</summary>
    V2Aligned,

    /// <summary>구버전 비정렬 패킷입니다. (요청 36바이트 / 응답 784바이트)</summary>
    V1Unaligned,
}
