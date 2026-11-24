using System;

namespace FieldLink.Robot;

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
