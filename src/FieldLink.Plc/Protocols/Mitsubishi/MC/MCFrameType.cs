namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜의 프레임 종류입니다. 헤더 구성만 다르고 요청 본문은 같습니다. (사양서 §3-1)</summary>
public enum MCFrameType
{
    /// <summary>3E 프레임. 시리얼 번호가 없습니다.</summary>
    Frame3E,

    /// <summary>4E 프레임. 요청과 응답을 짝짓는 시리얼 번호가 헤더에 들어갑니다.</summary>
    Frame4E,
}
