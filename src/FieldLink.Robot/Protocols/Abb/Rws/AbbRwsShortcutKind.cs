namespace FieldLink.Robot.Protocols.Abb.Rws;

/// <summary>ABB RWS 단축 주소(Shortcut Address) 종류입니다.</summary>
public enum AbbRwsShortcutKind
{
    /// <summary>컨트롤러 상태 조회 (<c>/rw/panel/ctrlstate</c>).</summary>
    CtrlState,

    /// <summary>모션 시스템 오류 상태 조회 (<c>/rw/motionsystem/errorstate</c>).</summary>
    ErrorState,

    /// <summary>조인트 목표 각도 조회 (<c>/rw/motionsystem/mechunits/ROB_1/jointtarget</c>).</summary>
    JointTarget,

    /// <summary>조인트 목표 각도 별칭 조회 (<c>jointtarget</c>과 동일).</summary>
    PhysicalJoints,

    /// <summary>속도 비율 조회 (<c>/rw/panel/speedratio</c>).</summary>
    SpeedRatio,

    /// <summary>운전 모드 조회 (<c>/rw/panel/opmode</c>).</summary>
    OperationMode,

    /// <summary>IO 장치1(<c>D652_10</c>) 입력 조회.</summary>
    IoIn,

    /// <summary>IO 장치1(<c>D652_10</c>) 출력 조회.</summary>
    IoOut,

    /// <summary>IO 장치2(<c>BK5250</c>) 입력 조회.</summary>
    Io2In,

    /// <summary>IO 장치2(<c>BK5250</c>) 출력 조회.</summary>
    Io2Out,

    /// <summary>이벤트 로그 목록 조회.</summary>
    Log,

    /// <summary>컨트롤러 시스템 정보 조회 (<c>/rw/system</c>).</summary>
    System,

    /// <summary>로봇 목표 직교 좌표 조회 (<c>/rw/motionsystem/mechunits/ROB_1/robtarget</c>).</summary>
    RobTarget,

    /// <summary>서보 활성화 신호 조회 (<c>/rw/iosystem/signals/Local/DRV_1/DRV1K1</c>).</summary>
    ServoEnable,

    /// <summary>RAPID 프로그램 실행 상태 조회 (<c>/rw/rapid/execution</c>).</summary>
    RapidExecution,

    /// <summary>RAPID 태스크 목록 조회 (<c>/rw/rapid/tasks</c>).</summary>
    RapidTasks,
}
