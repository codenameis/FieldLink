using System;

namespace FieldLink.Transport;

/// <summary>전송 계층의 송수신·연결 실패를 알립니다.</summary>
/// <remarks>
/// 호출자 취소와 제한 시간 초과는 이 예외가 아니라 <see cref="OperationCanceledException"/>으로 전달됩니다.
/// </remarks>
public sealed class TransportException : Exception
{
    /// <summary>실패 종류와 설명으로 예외를 만듭니다.</summary>
    /// <param name="failure">실패 종류입니다.</param>
    /// <param name="message">사람이 읽을 수 있는 설명입니다.</param>
    /// <param name="isRequestSent">요청 바이트가 일부라도 전송되었는지 여부입니다.</param>
    /// <param name="innerException">원인 예외입니다.</param>
    public TransportException(TransportFailure failure, string message, bool isRequestSent = false, Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
        IsRequestSent = isRequestSent;
    }

    /// <summary>실패 종류입니다.</summary>
    public TransportFailure Failure { get; }

    /// <summary>요청 바이트가 일부라도 장치로 나갔는지 여부입니다.</summary>
    /// <remarks>true이면 쓰기 명령이 장치에서 실행되었을 수 있습니다. 자동 재전송 판단에 사용하지 마세요.</remarks>
    public bool IsRequestSent { get; }
}
