namespace FieldLink.Robot.Protocols.Estun.Modbus;

/// <summary>Estun 컨트롤러 레지스터 <c>51</c>(40052)에 기록하는 원격 제어 명령 코드입니다.</summary>
public enum EstunCommandCode : short
{
    /// <summary>프로그램 시작 (<c>0x0004</c>).</summary>
    StartProgram = 0x0004,

    /// <summary>프로그램 정지 (<c>0x0008</c>).</summary>
    StopProgram = 0x0008,

    /// <summary>에러 리셋 (<c>0x0010</c>).</summary>
    ResetError = 0x0010,

    /// <summary>프로젝트 로드 (<c>0x0080</c>).</summary>
    LoadProject = 0x0080,

    /// <summary>프로젝트 언로드 (<c>0x0100</c>).</summary>
    UnregisterProject = 0x0100,

    /// <summary>글로벌 속도 설정 (<c>0x0200</c>).</summary>
    SetGlobalSpeed = 0x0200,

    /// <summary>명령 상태 재시작 (<c>0x0400</c>).</summary>
    RestartCommandStatus = 0x0400,
}
