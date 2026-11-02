using System;
using FieldLink.Plc.Protocols.Modbus;

namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>가상 PLC 가 요청을 거절할 때 쓰는 내부 예외입니다.</summary>
internal sealed class ModbusServerException : Exception
{
    public ModbusServerException(ModbusExceptionCode code)
        : base(code.GetDescription())
    {
        Code = code;
    }

    public ModbusExceptionCode Code { get; }
}
