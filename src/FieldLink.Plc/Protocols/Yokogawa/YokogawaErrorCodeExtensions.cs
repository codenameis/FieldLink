namespace FieldLink.Plc.Protocols.Yokogawa;

/// <summary><see cref="YokogawaErrorCode"/>에 대한 설명 확장 메서드입니다.</summary>
public static class YokogawaErrorCodeExtensions
{
    /// <summary>종료 코드에 해당하는 한국어 설명을 반환합니다.</summary>
    /// <param name="errorCode">장치 응답에서 읽은 종료 코드입니다.</param>
    /// <returns>종료 코드의 의미 설명입니다.</returns>
    public static string GetDescription(this YokogawaErrorCode errorCode) => errorCode switch
    {
        YokogawaErrorCode.Success => "정상 완료",
        YokogawaErrorCode.CpuNumberError => "CPU 번호 오류입니다. (1~4 범위 초과, ER01)",
        YokogawaErrorCode.CommandError => "명령 오류입니다. (명령 없음 또는 실행 불가, ER02)",
        YokogawaErrorCode.DeviceError => "디바이스 오류입니다. (해당 디바이스 없음, ER03)",
        YokogawaErrorCode.SettingValueOutOfRange => "설정값 범위 오류입니다. (ER04)",
        YokogawaErrorCode.DataCountOutOfRange => "데이터 개수 범위 오류입니다. (ER05)",
        YokogawaErrorCode.MonitorError => "모니터 오류입니다. (ER06)",
        YokogawaErrorCode.ParameterError => "파라미터 오류입니다. (ER08)",
        YokogawaErrorCode.SequenceIdError => "ID 오류입니다. (ER09)",
        YokogawaErrorCode.CpuNoResponse => "시퀀스 CPU 무응답(타임아웃) 오류입니다. (ER51)",
        YokogawaErrorCode.CpuProcessingError => "시퀀스 CPU 처리 오류입니다. (ER52)",
        _ => $"Yokogawa 종료 코드 오류: 0x{(byte)errorCode:X2}",
    };
}
