using System;

namespace FieldLink.Plc;

/// <summary>값을 돌려주는 PLC 작업(읽기 등)의 결과입니다. 생성 후 바뀌지 않습니다.</summary>
/// <typeparam name="T">작업 결과 값의 형식입니다.</typeparam>
public sealed class PlcResult<T> : PlcResult
{
    private readonly T _value;

    internal PlcResult(T value, PlcError? error)
        : base(error)
    {
        _value = value;
    }

    /// <summary>작업 결과 값입니다.</summary>
    /// <exception cref="InvalidOperationException">실패 결과에서 읽을 때. 먼저 <see cref="PlcResult.IsSuccess"/>를 확인하세요.</exception>
    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException($"실패한 결과에는 값이 없습니다. {Error}");
}
