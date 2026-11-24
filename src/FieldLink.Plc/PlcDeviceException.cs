namespace FieldLink.Plc;

/// <summary>장치가 정상 형식의 응답으로 요청을 거절했음을 알립니다.</summary>
/// <remarks>손상된 응답은 이 예외가 아니라 <see cref="PlcErrorKind.InvalidResponse"/>로 알립니다.</remarks>
public sealed class PlcDeviceException : PlcException
{
    /// <summary>장치 오류 코드와 설명으로 예외를 만듭니다.</summary>
    /// <param name="deviceErrorCode">장치가 보낸 오류 코드입니다.</param>
    /// <param name="message">오류 코드의 의미입니다. 제조사 매뉴얼의 표현을 사용합니다.</param>
    public PlcDeviceException(int deviceErrorCode, string message)
        : base(PlcErrorKind.DeviceError, message)
    {
        DeviceErrorCode = deviceErrorCode;
    }

    /// <summary>장치가 보낸 오류 코드입니다.</summary>
    public int DeviceErrorCode { get; }
}
