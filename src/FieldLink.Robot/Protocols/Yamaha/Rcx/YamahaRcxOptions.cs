using System;

namespace FieldLink.Robot.Protocols.Yamaha.Rcx;

/// <summary>YAMAHA RCX TCP ASCII 클라이언트에 사용하는 불변 설정입니다.</summary>
public sealed class YamahaRcxOptions
{
    private const int DefaultMaximumLineLength = 4096;
    private const int MinimumLineLength = 3;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _timeout = DefaultTimeout;
    private readonly int _maximumLineLength = DefaultMaximumLineLength;

    /// <summary>명령 응답 대기 제한 시간입니다. 사양서 기준 기본값은 30초입니다.</summary>
    public TimeSpan Timeout
    {
        get => _timeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "제한 시간은 0보다 커야 합니다.");
            }

            _timeout = value;
        }
    }

    /// <summary>단일 응답 라인의 최대 허용 바이트 길이입니다. 기본값은 4096입니다.</summary>
    public int MaximumLineLength
    {
        get => _maximumLineLength;
        init
        {
            if (value < MinimumLineLength)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"최대 라인 길이는 {MinimumLineLength} 이상이어야 합니다.");
            }

            _maximumLineLength = value;
        }
    }
}
