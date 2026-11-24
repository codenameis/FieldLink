namespace FieldLink.Plc.Protocols.Fatek;

/// <summary><see cref="FatekErrorCode"/>의 의미를 설명하는 확장 메서드입니다.</summary>
public static class FatekErrorCodeExtensions
{
    /// <summary>오류 코드에 대한 공식 매뉴얼 설명을 반환합니다.</summary>
    public static string GetDescription(this FatekErrorCode errorCode) => errorCode switch
    {
        FatekErrorCode.ErrorFree => "정상 완료 (Error free)",
        FatekErrorCode.IllegalValue => "데이터 값 범위 초과 (Illegal value)",
        FatekErrorCode.IllegalFormat => "포맷 오류 또는 쓰기 금지 / 명령 실행 불가 (Illegal format / write protected)",
        FatekErrorCode.InvalidCommandCode => "정의되지 않은 명령 코드 (Invalid command code)",
        FatekErrorCode.LadderChecksumError => "RUN 전환 불가 — 래더 체크섬 불일치",
        FatekErrorCode.PlcIdMismatch => "RUN 전환 불가 — PLC ID와 래더 ID 불일치",
        FatekErrorCode.SyntaxCheckError => "RUN 전환 불가 — 래더 문법 오류",
        FatekErrorCode.FunctionNotSupported => "RUN 전환 불가 — 미지원 명령어 포함",
        FatekErrorCode.IllegalAddress => "컴포넌트 기호 또는 주소 범위 초과 (Illegal address)",
        _ => "알 수 없는 FATEK 오류",
    };
}
