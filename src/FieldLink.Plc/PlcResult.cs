using System;
using System.Diagnostics.CodeAnalysis;

namespace FieldLink.Plc;

/// <summary>값이 없는 PLC 작업(쓰기, 연결 등)의 결과입니다. 생성 후 바뀌지 않습니다.</summary>
/// <remarks>
/// 통신·장치 실패처럼 현장에서 늘 일어날 수 있는 실패는 예외 대신 이 결과로 돌려줍니다.
/// 호출자 실수(잘못된 인수, Open 전 호출)는 예외로 즉시 알립니다.
/// </remarks>
public class PlcResult
{
    private static readonly PlcResult SuccessInstance = new(null);

    /// <summary>파생 형식이 성공 또는 실패 상태를 지정해 만듭니다.</summary>
    /// <param name="error">실패 정보입니다. 성공이면 null입니다.</param>
    protected PlcResult(PlcError? error)
    {
        Error = error;
    }

    /// <summary>작업이 성공했는지 여부입니다.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>실패 정보입니다. 성공이면 null입니다.</summary>
    public PlcError? Error { get; }

    /// <summary>성공 결과를 반환합니다.</summary>
    public static PlcResult Success() => SuccessInstance;

    /// <summary>값을 담은 성공 결과를 만듭니다.</summary>
    /// <typeparam name="T">값 형식입니다.</typeparam>
    /// <param name="value">작업 결과 값입니다.</param>
    public static PlcResult<T> Success<T>(T value) => new(value, null);

    /// <summary>실패 결과를 만듭니다.</summary>
    /// <param name="error">실패 정보입니다.</param>
    public static PlcResult Failure(PlcError error) =>
        new(error ?? throw new ArgumentNullException(nameof(error)));

    /// <summary>값 형식을 지정한 실패 결과를 만듭니다.</summary>
    /// <typeparam name="T">성공했다면 담았을 값의 형식입니다.</typeparam>
    /// <param name="error">실패 정보입니다.</param>
    public static PlcResult<T> Failure<T>(PlcError error) =>
        new(default!, error ?? throw new ArgumentNullException(nameof(error)));

    /// <inheritdoc/>
    public override string ToString() => IsSuccess ? "Success" : Error.ToString();
}

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
