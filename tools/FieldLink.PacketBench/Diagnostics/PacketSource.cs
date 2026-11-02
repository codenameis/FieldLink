namespace FieldLink.PacketBench.Diagnostics;

/// <summary>프레임을 기록한 쪽입니다.</summary>
internal enum PacketSource
{
    /// <summary>이 프로그램의 클라이언트 전송입니다.</summary>
    Client,

    /// <summary>내장 가상 PLC 서버가 주고받은 프레임입니다.</summary>
    VirtualPlc,
}
