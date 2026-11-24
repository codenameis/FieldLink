using System;
using System.Text;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA High-Speed Ethernet (<c>YERC</c>) UDP 클라이언트의 불변 설정입니다.</summary>
public sealed class YercUdpOptions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _timeout = DefaultTimeout;
    private readonly Encoding _stringEncoding = Encoding.ASCII;

    /// <summary>단일 UDP 요청-응답 제한 시간입니다. 기본값은 5초입니다.</summary>
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

    /// <summary>알람 및 문자열 변수 디코딩에 사용할 인코딩입니다. 기본값은 ASCII입니다.</summary>
    public Encoding StringEncoding
    {
        get => _stringEncoding;
        init => _stringEncoding = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>배치 읽기/쓰기(<c>0x301..0x306</c>) 시 4바이트 개수 헤더를 포함할지 여부입니다. 기본값은 true입니다.</summary>
    public bool IncludeBatchCountHeader { get; init; } = true;
}
