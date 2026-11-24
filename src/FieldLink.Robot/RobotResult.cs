using System;
using System.Diagnostics.CodeAnalysis;

namespace FieldLink.Robot;

/// <summary>값이 없는 로봇 통신 작업(연결, 쓰기, 제어 명령 등)의 결과입니다.</summary>
public class RobotResult
{
    private static readonly RobotResult SuccessInstance = new(null);

    /// <summary>오류 정보로 결과 인스턴스를 초기화합니다.</summary>
    /// <param name="error">실패 정보입니다. 성공이면 null입니다.</param>
    protected RobotResult(RobotError? error)
    {
        Error = error;
    }

    /// <summary>작업이 성공했는지 여부입니다.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>실패 정보입니다. 성공이면 null입니다.</summary>
    public RobotError? Error { get; }

    /// <summary>성공 결과를 반환합니다.</summary>
    public static RobotResult Success() => SuccessInstance;

    /// <summary>결과 값을 포함한 성공 결과를 생성합니다.</summary>
    /// <typeparam name="T">결과 값의 형식입니다.</typeparam>
    /// <param name="value">성공 결과 값입니다.</param>
    public static RobotResult<T> Success<T>(T value) => new(value, null);

    /// <summary>실패 결과를 생성합니다.</summary>
    /// <param name="error">실패 원인 정보입니다.</param>
    public static RobotResult Failure(RobotError error) =>
        new(error ?? throw new ArgumentNullException(nameof(error)));

    /// <summary>결과 값 형식을 지정한 실패 결과를 생성합니다.</summary>
    /// <typeparam name="T">성공 시 기대했던 값 형식입니다.</typeparam>
    /// <param name="error">실패 원인 정보입니다.</param>
    public static RobotResult<T> Failure<T>(RobotError error) =>
        new(default!, error ?? throw new ArgumentNullException(nameof(error)));

    /// <inheritdoc />
    public override string ToString() => IsSuccess ? "Success" : Error.ToString();
}

/// <summary>결과 값을 포함하는 로봇 통신 작업 결과입니다.</summary>
/// <typeparam name="T">작업 결과 값의 형식입니다.</typeparam>
public sealed class RobotResult<T> : RobotResult
{
    private readonly T _value;

    internal RobotResult(T value, RobotError? error)
        : base(error)
    {
        _value = value;
    }

    /// <summary>성공한 작업의 결과 값입니다.</summary>
    /// <exception cref="InvalidOperationException">실패한 결과에서 값을 읽으려 할 때.</exception>
    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException($"실패한 결과에는 값이 없습니다. {Error}");
}
