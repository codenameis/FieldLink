namespace FieldLink.Plc.Protocols.Keyence;

/// <summary>Keyence KV 상위 링크(Host Link) 응답의 오류 코드입니다. (사양서 Part A §A.2.3)</summary>
public enum KeyenceHostLinkErrorCode
{
    /// <summary>디바이스 번호 이상 — 범위 초과, 존재하지 않는 디바이스 또는 모니터 미등록 (E0)</summary>
    DeviceNumberError = 0,

    /// <summary>커맨드 이상 — 미지원 명령, 파라미터 개수 오류 또는 포맷 접미사 오류 (E1)</summary>
    CommandError = 1,

    /// <summary>프로그램 미등록 — CPU 프로그램 부재 또는 현재 모드에서 실행 불가 (E2)</summary>
    ProgramNotRegistered = 2,

    /// <summary>쓰기 금지 — 메모리 쓰기 금지 설정 상태 (E4)</summary>
    WriteProtected = 4,

    /// <summary>본체 에러 — CPU 유닛 에러 미해제 (E5)</summary>
    UnitError = 5,

    /// <summary>코멘트 없음 — RDC 요청 대상 디바이스의 등록된 주석 없음 (E6)</summary>
    NoComment = 6,
}
