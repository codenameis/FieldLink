namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary>Yokogawa FA-M3 Link 프로토콜 응답의 종료 코드입니다. (IM 34M06H24-08E Table 5.4, 사양서 §2.3)</summary>
public enum YokogawaErrorCode : byte
{
    /// <summary>정상 완료 (00)</summary>
    Success = 0x00,

    /// <summary>CPU 번호 오류 — 1~4 범위 초과 (ER01)</summary>
    CpuNumberError = 0x01,

    /// <summary>명령 오류 — 명령 없음 또는 실행 불가 (ER02)</summary>
    CommandError = 0x02,

    /// <summary>디바이스 오류 — 해당 디바이스 없음 (ER03)</summary>
    DeviceError = 0x03,

    /// <summary>설정값 범위 오류 — 비트가 0/1이 아님 (ER04)</summary>
    SettingValueOutOfRange = 0x04,

    /// <summary>데이터 개수 범위 오류 — 비트/워드 수 초과 또는 파라미터 수 불일치 (ER05)</summary>
    DataCountOutOfRange = 0x05,

    /// <summary>모니터 오류 — 모니터 미지정 상태에서 실행 (ER06)</summary>
    MonitorError = 0x06,

    /// <summary>파라미터 오류 — 잘못된 파라미터 (ER08)</summary>
    ParameterError = 0x08,

    /// <summary>ID 오류 — 잘못된 시퀀스 번호 (ER09)</summary>
    SequenceIdError = 0x09,

    /// <summary>시퀀스 CPU 무응답 — 타임아웃 (ER51)</summary>
    CpuNoResponse = 0x51,

    /// <summary>시퀀스 CPU 처리 오류 (ER52)</summary>
    CpuProcessingError = 0x52,
}
