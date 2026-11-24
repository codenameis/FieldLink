using System;

namespace FieldLink.Plc;

/// <summary>프로토콜 구현이 통신·장치 실패를 알릴 때 던지는 예외입니다.</summary>
/// <remarks>
/// 프로토콜 내부(Protocol, ResponseParser)에서만 던지고, <c>PlcClient</c>가 경계에서 한 번만
/// <see cref="PlcResult"/>로 바꿉니다. 사용자 코드가 직접 받는 일은 저수준 API를 쓸 때뿐입니다.
/// </remarks>
public class PlcException : Exception
{
    internal const string CompletedCountDataKey = "FieldLink.Plc.CompletedCount";
    private readonly int _completedCount;

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
    /// <exception cref="ArgumentOutOfRangeException">값이 0보다 작을 때.</exception>
    public int CompletedCount
    {
        get => _completedCount;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "완료된 원소 수는 0 이상이어야 합니다.");
            }

            _completedCount = value;
        }
    }

    internal static int GetCompletedCount(Exception exception)
    {
        if (exception is PlcException plcException)
        {
            return plcException.CompletedCount;
        }

        if (exception.Data.Contains(CompletedCountDataKey) && exception.Data[CompletedCountDataKey] is int count && count > 0)
        {
            return count;
        }

        return 0;
    }
}
