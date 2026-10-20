namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜의 디바이스 지정 형식을 가르는 PLC 시리즈 구분입니다. (사양서 §3-2, §3-3)</summary>
public enum MCSeries
{
    /// <summary>QnA · Q · L · iQ-L 시리즈. 디바이스 번호 3바이트, 디바이스 코드 1바이트, 서브커맨드 0000/0001.</summary>
    QL,

    /// <summary>iQ-R 시리즈. 디바이스 번호 4바이트, 디바이스 코드 2바이트, 서브커맨드 0002/0003.</summary>
    Iqr,
}
