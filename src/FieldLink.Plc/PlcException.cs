using System;

namespace FieldLink.Plc;

/// <summary>프로토콜 구현이 통신·장치 실패를 알릴 때 던지는 예외입니다.</summary>
/// <remarks>
/// 프로토콜 내부(Protocol, ResponseParser)에서만 던지고, <c>PlcClient</c>가 경계에서 한 번만
/// <see cref="PlcResult"/>로 바꿉니다. 사용자 코드가 직접 받는 일은 저수준 API를 쓸 때뿐입니다.
/// </remarks>
public class PlcException : Exception
{
    /// <summary>실패 분류와 설명으로 예외를 만듭니다.</summary>
    /// <param name="kind">실패 분류입니다.</param>
    /// <param name="message">사람이 읽을 수 있는 설명입니다.</param>
    /// <param name="innerException">원인 예외입니다.</param>
    public PlcException(PlcErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>실패 분류입니다.</summary>
    public PlcErrorKind Kind { get; }

    /// <summary>분할 전송에서 실패 전에 완료가 확인된 원소 수입니다.</summary>
    public int CompletedCount { get; init; }
}

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
