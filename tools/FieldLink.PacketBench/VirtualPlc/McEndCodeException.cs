using System;

namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>가상 MC PLC 가 요청을 거절할 때 쓰는 내부 예외입니다.</summary>
internal sealed class McEndCodeException : Exception
{
    public McEndCodeException(ushort endCode)
        : base($"MC 종료 코드 0x{endCode:X4}")
    {
        EndCode = endCode;
    }

    public ushort EndCode { get; }
}
