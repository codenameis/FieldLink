namespace FieldLink.Plc.Protocols.Fatek;

/// <summary>FATEK 통신 프로토콜 응답의 상태 및 오류 코드입니다. (사양서 §2.2, ASCII 문자 코드)</summary>
public enum FatekErrorCode
{
    /// <summary>정상 완료 ('0')</summary>
    ErrorFree = '0',

    /// <summary>데이터 값 범위 초과 ('2')</summary>
    IllegalValue = '2',

    /// <summary>포맷 오류 또는 명령 실행 불가 / 쓰기 금지 ('3')</summary>
    IllegalFormat = '3',

    /// <summary>정의되지 않은 명령 코드 ('4')</summary>
    InvalidCommandCode = '4',

    /// <summary>RUN 전환 불가 — 래더 프로그램 체크섬 불일치 ('5')</summary>
    LadderChecksumError = '5',

    /// <summary>RUN 전환 불가 — PLC ID와 래더 ID 불일치 ('6')</summary>
    PlcIdMismatch = '6',

    /// <summary>RUN 전환 불가 — 래더 프로그램 문법 오류 ('7')</summary>
    SyntaxCheckError = '7',

    /// <summary>RUN 전환 불가 — 지원하지 않는 명령어 포함 ('9')</summary>
    FunctionNotSupported = '9',

    /// <summary>컴포넌트 기호 또는 주소 범위 초과 ('A')</summary>
    IllegalAddress = 'A',
}
