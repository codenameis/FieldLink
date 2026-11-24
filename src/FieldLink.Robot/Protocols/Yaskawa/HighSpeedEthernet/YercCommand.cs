namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA High-Speed Ethernet (<c>YERC</c>) 명령 번호 상수입니다.</summary>
public enum YercCommand : ushort
{
    /// <summary>현재 알람 조회 (<c>0x70</c>).</summary>
    ReadAlarm = 0x70,

    /// <summary>이력 알람 조회 (<c>0x71</c>).</summary>
    ReadHistoryAlarm = 0x71,

    /// <summary>로봇 상태 비트 조회 (<c>0x72</c>).</summary>
    ReadStats = 0x72,

    /// <summary>실행 JOB 정보 조회 (<c>0x73</c>).</summary>
    ReadJobSequence = 0x73,

    /// <summary>현재 자세 조회 (<c>0x75</c>).</summary>
    ReadPose = 0x75,

    /// <summary>각 축 토크 조회 (<c>0x77</c>).</summary>
    ReadTorque = 0x77,

    /// <summary>단일 I/O 읽기·쓰기 (<c>0x78</c>).</summary>
    SingleIo = 0x78,

    /// <summary>단일 레지스터 <c>M</c> 읽기·쓰기 (<c>0x79</c>).</summary>
    SingleRegister = 0x79,

    /// <summary>단일 바이트 변수 <c>B</c> 읽기·쓰기 (<c>0x7A</c>).</summary>
    SingleByte = 0x7A,

    /// <summary>단일 정수 변수 <c>I</c> 읽기·쓰기 (<c>0x7B</c>).</summary>
    SingleInteger = 0x7B,

    /// <summary>단일 배정수 변수 <c>D</c> 읽기·쓰기 (<c>0x7C</c>).</summary>
    SingleDoubleInteger = 0x7C,

    /// <summary>단일 실수 변수 <c>R</c> 읽기·쓰기 (<c>0x7D</c>).</summary>
    SingleReal = 0x7D,

    /// <summary>단일 문자열 변수 <c>S</c> 읽기·쓰기 (<c>0x7E</c>).</summary>
    SingleString = 0x7E,

    /// <summary>알람 리셋 및 에러 취소 (<c>0x82</c>).</summary>
    ResetOrCancel = 0x82,

    /// <summary>HOLD, 서보 전원, 인터락 제어 (<c>0x83</c>).</summary>
    HoldServoOrLock = 0x83,

    /// <summary>사이클 모드 선택 (<c>0x84</c>).</summary>
    Cycle = 0x84,

    /// <summary>펜던트 메시지 표시 (<c>0x85</c>).</summary>
    DisplayMessage = 0x85,

    /// <summary>JOB 시작 (<c>0x86</c>).</summary>
    StartJob = 0x86,

    /// <summary>JOB 선택 (<c>0x87</c>).</summary>
    SelectJob = 0x87,

    /// <summary>관리 시간 조회 (<c>0x88</c>).</summary>
    ManagementTime = 0x88,

    /// <summary>시스템 정보 조회 (<c>0x89</c>).</summary>
    SystemInfo = 0x89,

    /// <summary>배치 I/O 읽기·쓰기 (<c>0x300</c>).</summary>
    BatchIo = 0x300,

    /// <summary>배치 레지스터 <c>M</c> 읽기·쓰기 (<c>0x301</c>).</summary>
    BatchRegister = 0x301,

    /// <summary>배치 바이트 변수 <c>B</c> 읽기·쓰기 (<c>0x302</c>).</summary>
    BatchByte = 0x302,

    /// <summary>배치 정수 변수 <c>I</c> 읽기·쓰기 (<c>0x303</c>).</summary>
    BatchInteger = 0x303,

    /// <summary>배치 배정수 변수 <c>D</c> 읽기·쓰기 (<c>0x304</c>).</summary>
    BatchDoubleInteger = 0x304,

    /// <summary>배치 실수 변수 <c>R</c> 읽기·쓰기 (<c>0x305</c>).</summary>
    BatchReal = 0x305,

    /// <summary>배치 문자열 변수 <c>S</c> 읽기·쓰기 (<c>0x306</c>).</summary>
    BatchString = 0x306,
}
