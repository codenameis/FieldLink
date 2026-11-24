namespace FieldLink.Plc.Protocols.FujiElectric;

/// <summary><see cref="FujiElectricSpbErrorCode"/>의 공식 매뉴얼 의미를 반환하는 확장 메서드입니다.</summary>
public static class FujiElectricSpbErrorCodeExtensions
{
    /// <summary>오류 코드에 대한 공식 매뉴얼 설명을 반환합니다.</summary>
    public static string GetDescription(this FujiElectricSpbErrorCode errorCode) => errorCode switch
    {
        FujiElectricSpbErrorCode.Normal => "정상 완료",
        FujiElectricSpbErrorCode.RomAreaWriteError => "ROM 영역 쓰기 요청 오류",
        FujiElectricSpbErrorCode.InvalidCommand => "정의되지 않았거나 처리 불가한 명령/서브명령",
        FujiElectricSpbErrorCode.DataAreaParameterError => "데이터 영역 매개변수 지정 오류",
        FujiElectricSpbErrorCode.TransferInterlockOccupied => "타 로더/프로그래머의 전송 인터록 점유",
        FujiElectricSpbErrorCode.InvalidModuleNumber => "모듈 번호 지정 오류",
        FujiElectricSpbErrorCode.SearchItemNotFound => "검색 항목 없음",
        FujiElectricSpbErrorCode.AddressOutOfRange => "쓰기 주소가 모듈 메모리 범위 초과",
        FujiElectricSpbErrorCode.ProgramErrorCannotRun => "프로그램 오류로 인해 RUN 전환 불가",
        FujiElectricSpbErrorCode.PasswordMismatch => "암호 불일치",
        _ => "알 수 없는 Fuji SPB 오류",
    };
}
