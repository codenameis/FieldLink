namespace FieldLink.Plc.Protocols.Keyence;

/// <summary><see cref="KeyenceHostLinkErrorCode"/>에 대한 설명 확장 메서드입니다.</summary>
public static class KeyenceHostLinkErrorCodeExtensions
{
    /// <summary>오류 코드에 해당하는 한국어 설명을 반환합니다.</summary>
    /// <param name="errorCode">장치 응답에서 읽은 오류 코드입니다.</param>
    /// <returns>오류 코드의 의미 설명입니다.</returns>
    public static string GetDescription(this KeyenceHostLinkErrorCode errorCode) => errorCode switch
    {
        KeyenceHostLinkErrorCode.DeviceNumberError => "디바이스 번호 이상 (범위 초과 또는 미등록 디바이스)",
        KeyenceHostLinkErrorCode.CommandError => "커맨드 이상 (미지원 명령 또는 파라미터 형식 오류)",
        KeyenceHostLinkErrorCode.ProgramNotRegistered => "프로그램 미등록 (CPU에 프로그램 부재 또는 모드 오류)",
        KeyenceHostLinkErrorCode.WriteProtected => "쓰기 금지 (메모리 쓰기 금지 상태)",
        KeyenceHostLinkErrorCode.UnitError => "본체 에러 (CPU 유닛 에러 미해제)",
        KeyenceHostLinkErrorCode.NoComment => "코멘트 없음",
        _ => $"알 수 없는 Keyence 상위 링크 에러(E{(int)errorCode})",
    };
}
