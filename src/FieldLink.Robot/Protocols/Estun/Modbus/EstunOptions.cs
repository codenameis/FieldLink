using System;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

/// <summary>Estun Modbus TCP 로봇 클라이언트의 국번, Keep-Alive, 핸드셰이크 불변 설정입니다.</summary>
public sealed class EstunOptions
{
    private const byte DefaultStation = 0x01;
    private const int DefaultMaxAuthorityPollAttempts = 20;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultKeepAliveInitialDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultHandshakePollInterval = TimeSpan.FromMilliseconds(100);

    private readonly TimeSpan _timeout = DefaultTimeout;
    private readonly TimeSpan _keepAliveInitialDelay = DefaultKeepAliveInitialDelay;
    private readonly TimeSpan _keepAliveInterval = DefaultKeepAliveInterval;
    private readonly TimeSpan _handshakePollInterval = DefaultHandshakePollInterval;
    private readonly int _maxAuthorityPollAttempts = DefaultMaxAuthorityPollAttempts;

    /// <summary>Modbus TCP Unit ID(국번)입니다. 기본값은 <c>0x01</c>입니다.</summary>
    public byte Station { get; init; } = DefaultStation;

    /// <summary>단일 요청 또는 제어 핸드셰이크 제한 시간입니다. 기본값은 5초입니다.</summary>
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

    /// <summary>백그라운드 Keep-Alive 폴링 시작 지연 시간입니다. 기본값은 3초입니다.</summary>
    public TimeSpan KeepAliveInitialDelay
    {
        get => _keepAliveInitialDelay;
        init
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "초기 지연 시간은 0 이상이어야 합니다.");
            }

            _keepAliveInitialDelay = value;
        }
    }

    /// <summary>백그라운드 Keep-Alive 폴링 주기입니다. <see cref="TimeSpan.Zero"/>이면 비활성화됩니다.</summary>
    public TimeSpan KeepAliveInterval
    {
        get => _keepAliveInterval;
        init
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Keep-Alive 주기는 0 이상이어야 합니다.");
            }

            _keepAliveInterval = value;
        }
    }

    /// <summary>6단계 제어 핸드셰이크 폴링 간격입니다. 기본값은 100ms입니다.</summary>
    public TimeSpan HandshakePollInterval
    {
        get => _handshakePollInterval;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "폴링 간격은 0보다 커야 합니다.");
            }

            _handshakePollInterval = value;
        }
    }

    /// <summary>권한 확인(Reg 18 = <c>0x0801</c>) 최대 폴링 횟수입니다. 기본값은 20회입니다.</summary>
    public int MaxAuthorityPollAttempts
    {
        get => _maxAuthorityPollAttempts;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 시도 횟수는 1 이상이어야 합니다.");
            }

            _maxAuthorityPollAttempts = value;
        }
    }

    /// <summary>프로젝트 로드 시 쓰기 바이트를 워드 단위로 반전할지 여부입니다. 기본값은 false입니다.</summary>
    public bool SwapProjectNameWriteBytes { get; init; }
}
