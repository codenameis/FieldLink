namespace FieldLink.PacketBench.Diagnostics;

/// <summary>패킷이 흐른 방향입니다.</summary>
internal enum PacketDirection
{
    /// <summary>이 프로그램이 보낸 프레임입니다.</summary>
    Sent,

    /// <summary>이 프로그램이 받은 프레임입니다.</summary>
    Received,
}
