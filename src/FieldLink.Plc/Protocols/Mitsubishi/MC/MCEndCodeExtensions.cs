namespace FieldLink.Plc.Protocols.Mitsubishi.MC;

/// <summary><see cref="MCEndCode"/>의 의미를 사람이 읽을 문장으로 바꾸는 확장 메서드입니다.</summary>
public static class MCEndCodeExtensions
{
    /// <summary>종료 코드가 정상(0x0000)이 아니면 장치 거절 예외를 던집니다.</summary>
    /// <param name="endCodeValue">응답에서 읽은 종료 코드 값입니다.</param>
    /// <exception cref="PlcDeviceException">정상이 아닐 때. 오류 코드는 응답의 값 그대로입니다.</exception>
    public static void EnsureSuccess(ushort endCodeValue)
    {
        if (endCodeValue != (ushort)MCEndCode.Success)
        {
            throw new PlcDeviceException(endCodeValue, ((MCEndCode)endCodeValue).GetDescription());
        }
    }

    /// <summary>종료 코드의 의미를 돌려줍니다. 사양서에 없는 코드는 코드 값만 알려 줍니다.</summary>
    public static string GetDescription(this MCEndCode endCode)
    {
        return endCode switch
        {
            MCEndCode.Success => "정상 완료",
            MCEndCode.RequestLengthMismatch => "요청 데이터 길이가 실제 프레임 크기와 맞지 않습니다.",
            MCEndCode.WriteProhibitedWhileRunning => "RUN 중 쓰기 금지 설정 때문에 쓸 수 없습니다.",
            MCEndCode.PointCountExceeded => "한 번에 처리할 수 있는 점수 한도를 넘었습니다.",
            MCEndCode.DeviceRangeExceeded => "디바이스 주소가 유효 범위를 벗어났습니다.",
            MCEndCode.InvalidCommand => "커맨드 또는 서브커맨드가 잘못되었습니다.",
            MCEndCode.DeviceNotAccessible => "지정한 디바이스는 읽기·쓰기를 할 수 없습니다.",
            MCEndCode.InvalidRequest => "요청 내용에 오류가 있습니다.",
            MCEndCode.NotExecutableOnTarget => "대상 CPU 모듈에서 실행할 수 없는 요청입니다.",
            MCEndCode.InvalidRequestData => "요청 데이터 내용에 오류가 있습니다.",
            MCEndCode.DataLengthMismatch => "요청 데이터 길이가 디바이스 점수와 일치하지 않습니다.",
            _ => $"사양서에 없는 종료 코드입니다. (0x{(ushort)endCode:X4})"
        };
    }
}
