namespace FieldLink.Robot;

/// <summary>로봇 컨트롤러가 정상 형식의 프로토콜 오류 응답으로 요청을 거절했음을 알립니다.</summary>
public sealed class RobotDeviceException : RobotException
{
    /// <summary>장치 오류 코드와 진단 메시지로 예외를 초기화합니다.</summary>
    /// <param name="deviceErrorCode">장치가 반환한 오류 코드입니다.</param>
    /// <param name="message">오류 코드의 의미 설명입니다.</param>
    /// <param name="rawFrame">수신된 원본 응답 프레임입니다.</param>
    public RobotDeviceException(int deviceErrorCode, string message, byte[]? rawFrame = null)
        : base(RobotErrorKind.DeviceError, message, rawFrame)
    {
        DeviceErrorCode = deviceErrorCode;
    }

    /// <summary>로봇 컨트롤러가 반환한 프로토콜 오류 코드입니다.</summary>
    public int DeviceErrorCode { get; }
}
