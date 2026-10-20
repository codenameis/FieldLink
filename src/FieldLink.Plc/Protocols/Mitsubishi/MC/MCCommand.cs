namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary>MC 프로토콜 커맨드 코드입니다. (사양서 §3-2)</summary>
public enum MCCommand : ushort
{
    /// <summary>디바이스 일괄 읽기.</summary>
    BatchRead = 0x0401,

    /// <summary>디바이스 일괄 쓰기.</summary>
    BatchWrite = 0x1401,
}
