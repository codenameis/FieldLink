namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC 실행 태스크 정보(36바이트) 모델입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class FanucTask
{
    /// <summary>실행 중인 프로그램 이름입니다.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>현재 실행 라인 번호입니다.</summary>
    public short LineNumber { get; init; }

    /// <summary>태스크 실행 상태 코드입니다.</summary>
    public short State { get; init; }

    /// <summary>상위 호출 프로그램 이름입니다.</summary>
    public string ParentProgramName { get; init; } = string.Empty;
}
