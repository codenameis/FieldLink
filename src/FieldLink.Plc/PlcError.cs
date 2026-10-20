using System;

namespace FieldLink.Plc;

/// <summary>실패한 PLC 작업의 원인과 진행 정보입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class PlcError
{
    /// <summary>실패 정보를 만듭니다.</summary>
    /// <param name="kind">실패 분류입니다.</param>
    /// <param name="message">사람이 읽을 수 있는 설명입니다.</param>
    /// <param name="deviceErrorCode">장치가 보낸 오류 코드입니다. 장치 오류가 아니면 null입니다.</param>
    /// <param name="completedCount">분할 전송에서 실패 전에 완료가 확인된 원소 수입니다.</param>
    /// <param name="exception">원인 예외입니다.</param>
    public PlcError(PlcErrorKind kind, string message, int? deviceErrorCode = null, int completedCount = 0, Exception? exception = null)
    {
        if (completedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completedCount));
        }

        Kind = kind;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        DeviceErrorCode = deviceErrorCode;
        CompletedCount = completedCount;
        Exception = exception;
    }

    /// <summary>실패 분류입니다.</summary>
    public PlcErrorKind Kind { get; }

    /// <summary>사람이 읽을 수 있는 설명입니다.</summary>
    public string Message { get; }

    /// <summary>장치가 보낸 오류 코드입니다. (예: Modbus 예외 코드, MC 종료 코드, FINS 종료 코드)</summary>
    public int? DeviceErrorCode { get; }

    /// <summary>분할 전송에서 실패 전에 완료가 확인된 원소 수입니다.</summary>
    /// <remarks>분할 쓰기는 원자적이지 않습니다. 이 수 이후의 원소는 기록되었는지 알 수 없습니다.</remarks>
    public int CompletedCount { get; }

    /// <summary>원인 예외입니다. 없으면 null입니다.</summary>
    public Exception? Exception { get; }

    /// <inheritdoc/>
    public override string ToString() =>
        DeviceErrorCode is null ? $"{Kind}: {Message}" : $"{Kind}(0x{DeviceErrorCode:X4}): {Message}";
}
