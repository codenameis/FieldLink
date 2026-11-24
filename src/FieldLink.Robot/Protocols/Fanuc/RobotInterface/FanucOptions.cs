using System;
using System.Text;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC Robot Interface 클라이언트에 사용하는 불변 설정입니다.</summary>
public sealed class FanucOptions
{
    private const int DefaultClientId = 1024;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultSnapshotCacheDuration = TimeSpan.FromMilliseconds(100);

    private readonly TimeSpan _timeout = DefaultTimeout;
    private readonly TimeSpan _snapshotCacheDuration = DefaultSnapshotCacheDuration;
    private readonly Encoding _stringEncoding = Encoding.ASCII;

    /// <summary>Connect 핸드셰이크에 사용할 클라이언트 식별자입니다. 기본값은 1024입니다.</summary>
    public int ClientId { get; init; } = DefaultClientId;

    /// <summary>알람·작업명·문자열 레지스터 디코딩에 사용할 문자 인코딩입니다. 기본값은 ASCII입니다.</summary>
    public Encoding StringEncoding
    {
        get => _stringEncoding;
        init => _stringEncoding = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>단일 요청 제한 시간입니다. 기본값은 5초입니다.</summary>
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

    /// <summary>스냅샷 문자열 조회 시 직전 스냅샷을 재사용할 캐시 유지 시간입니다. 기본값은 100ms입니다.</summary>
    public TimeSpan SnapshotCacheDuration
    {
        get => _snapshotCacheDuration;
        init
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "캐시 유지 시간은 0 이상이어야 합니다.");
            }

            _snapshotCacheDuration = value;
        }
    }
}
