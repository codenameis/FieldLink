namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

/// <summary>YASKAWA <c>SAVEV</c> 명령에서 사용하는 컨트롤러 변수 유형 코드입니다.</summary>
public enum YrcVariableType
{
    /// <summary>바이트 변수 <c>B</c> (<c>0</c>).</summary>
    Byte = 0,

    /// <summary>정수 변수 <c>I</c> (<c>1</c>).</summary>
    Integer = 1,

    /// <summary>배정수 변수 <c>D</c> (<c>2</c>).</summary>
    DoubleInteger = 2,

    /// <summary>실수 변수 <c>R</c> (<c>3</c>).</summary>
    Real = 3,

    /// <summary>문자열 변수 <c>S</c> (<c>7</c>).</summary>
    String = 7,
}
