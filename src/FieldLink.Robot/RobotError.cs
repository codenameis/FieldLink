using System;

namespace FieldLink.Robot;

/// <summary>실패한 로봇 통신 작업의 상세 원인 정보입니다. 생성 후 바뀌지 않습니다.</summary>
public sealed class RobotError
{
    private readonly byte[]? _rawFrame;

    /// <summary>실패 분류와 진단 메시지로 오류 정보를 초기화합니다.</summary>
    /// <param name="kind">실패 분류입니다.</param>
    /// <param name="message">사람이 읽을 수 있는 진단 메시지입니다.</param>
    /// <param name="deviceErrorCode">장치가 반환한 오류 코드입니다. 장치 오류가 아니면 null입니다.</param>
    /// <param name="rawFrame">진단용 원본 송수신 프레임입니다. 없으면 null입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/>가 null일 때.</exception>
    public RobotError(
        RobotErrorKind kind,
        string message,
        int? deviceErrorCode = null,
        byte[]? rawFrame = null)
    {
        Kind = kind;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        DeviceErrorCode = deviceErrorCode;
        _rawFrame = rawFrame is not null ? (byte[])rawFrame.Clone() : null;
    }

    /// <summary>실패 분류입니다.</summary>
    public RobotErrorKind Kind { get; }

    /// <summary>사람이 읽을 수 있는 진단 메시지입니다.</summary>
    public string Message { get; }

    /// <summary>로봇 컨트롤러가 반환한 프로토콜 고유 오류 코드입니다.</summary>
    public int? DeviceErrorCode { get; }

    /// <summary>오류의 원인이 된 내부 예외입니다. 없으면 null입니다.</summary>
    public Exception? Exception { get; init; }

    /// <summary>진단용 원본 프레임 바이트 배열의 복사본을 반환합니다.</summary>
    /// <returns>원본 프레임이 있으면 복사본, 없으면 null입니다.</returns>
    public byte[]? GetRawFrame() => _rawFrame is not null ? (byte[])_rawFrame.Clone() : null;

    /// <inheritdoc />
    public override string ToString() =>
        DeviceErrorCode is null ? $"{Kind}: {Message}" : $"{Kind}(0x{DeviceErrorCode:X4}): {Message}";
}
