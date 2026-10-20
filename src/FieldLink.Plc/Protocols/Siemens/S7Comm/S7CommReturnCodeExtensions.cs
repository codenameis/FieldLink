namespace FieldLink.Plc.Protocols.Siemens.S7Comm;

/// <summary><see cref="S7CommReturnCode"/>를 해석하는 확장 메서드입니다.</summary>
public static class S7CommReturnCodeExtensions
{
    /// <summary>반환 코드가 성공이 아니면 장치 거절 예외를 던집니다.</summary>
    /// <param name="returnCodeValue">응답 데이터 구역에서 읽은 반환 코드 값입니다.</param>
    /// <exception cref="PlcDeviceException">성공(0xFF)이 아닐 때. 오류 코드는 응답의 값 그대로입니다.</exception>
    public static void EnsureSuccess(byte returnCodeValue)
    {
        if (returnCodeValue != (byte)S7CommReturnCode.Success)
        {
            throw new PlcDeviceException(returnCodeValue, ((S7CommReturnCode)returnCodeValue).GetDescription());
        }
    }

    /// <summary>반환 코드의 의미를 돌려줍니다. 사양서에 없는 코드는 값만 알려 줍니다.</summary>
    public static string GetDescription(this S7CommReturnCode returnCode)
    {
        return returnCode switch
        {
            S7CommReturnCode.Success => "성공",
            S7CommReturnCode.HardwareFault => "하드웨어 오류입니다.",
            S7CommReturnCode.AccessNotAllowed => "해당 객체에 접근이 허용되지 않습니다. S7-1200/1500은 PUT/GET 허용과 DB의 최적화 접근 해제가 필요합니다.",
            S7CommReturnCode.InvalidAddress => "주소가 올바르지 않습니다.",
            S7CommReturnCode.DataTypeNotSupported => "지원하지 않는 자료형입니다.",
            S7CommReturnCode.DataTypeInconsistent => "자료형이 일치하지 않습니다.",
            S7CommReturnCode.ObjectDoesNotExist => "객체가 존재하지 않습니다. DB 번호를 확인하세요.",
            _ => $"사양서에 없는 반환 코드입니다. (0x{(byte)returnCode:X2})"
        };
    }
}
