namespace FieldLink.Robot.Protocols.Kuka.VarProxy;

/// <summary>KUKAVARPROXY 바이너리 프로토콜 기능 코드입니다.</summary>
public enum KukaVarProxyFunctionCode : byte
{
    /// <summary>KRL 변수 읽기 (<c>0x00</c>).</summary>
    Read = 0x00,

    /// <summary>KRL 변수 쓰기 (<c>0x01</c>).</summary>
    Write = 0x01,
}
