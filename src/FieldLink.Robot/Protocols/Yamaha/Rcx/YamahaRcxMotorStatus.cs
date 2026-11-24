namespace FieldLink.Robot.Protocols.Yamaha.Rcx;

/// <summary>YAMAHA RCX 컨트롤러의 <c>@?MOTOR</c> 응답 상태 코드입니다.</summary>
public enum YamahaRcxMotorStatus
{
    /// <summary>모터 전원 OFF (<c>0</c>).</summary>
    Off = 0,

    /// <summary>모터 전원 ON, 일부 또는 전체 서보 OFF (<c>1</c>).</summary>
    On = 1,

    /// <summary>모터 전원 ON 및 모든 로봇 서보 ON (<c>2</c>).</summary>
    OnAllServosEnabled = 2,
}
