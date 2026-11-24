using System;

namespace FieldLink.Robot;

/// <summary>로봇 프로토콜 계층의 통신·파싱·미지원 오류를 알리는 내부 예외입니다.</summary>
public class RobotException : Exception
{
    private readonly byte[]? _rawFrame;

    /// <summary>오류 분류와 설명으로 예외를 초기화합니다.</summary>
    /// <param name="kind">실패 분류입니다.</param>
    /// <param name="message">진단 메시지입니다.</param>
    /// <param name="rawFrame">관련된 원본 프레임입니다. 없으면 null입니다.</param>
    /// <param name="innerException">원인 예외입니다.</param>
    public RobotException(
        RobotErrorKind kind,
        string message,
        byte[]? rawFrame = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        _rawFrame = rawFrame is not null ? (byte[])rawFrame.Clone() : null;
    }

    /// <summary>실패 분류입니다.</summary>
    public RobotErrorKind Kind { get; }

    /// <summary>프로토콜 또는 HTTP 상태 등에서 전달된 선택적 오류 코드입니다.</summary>
    public int? ProtocolErrorCode { get; init; }

    /// <summary>진단용 원본 프레임 바이트 배열의 복사본을 반환합니다.</summary>
    /// <returns>원본 프레임이 있으면 복사본, 없으면 null입니다.</returns>
    public byte[]? GetRawFrame() => _rawFrame is not null ? (byte[])_rawFrame.Clone() : null;
}
