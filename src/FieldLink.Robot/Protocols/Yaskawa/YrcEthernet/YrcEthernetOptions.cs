using System;
using System.Text;

namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

/// <summary>YASKAWA YRC1000/DX200 Ethernet Server TCP 클라이언트의 불변 설정입니다.</summary>
public sealed class YrcEthernetOptions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _timeout = DefaultTimeout;
    private readonly Encoding _encoding = Encoding.ASCII;

    /// <summary>로봇 모델 구분입니다. (<c>Yrc1000</c> 6축 / <c>Yrc100</c> 7축)</summary>
    public YrcModelType ModelType { get; init; } = YrcModelType.Yrc1000;

    /// <summary>단일 명령 제한 시간입니다. 기본값은 5초입니다.</summary>
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

    /// <summary>ASCII 명령 및 응답 텍스트 인코딩입니다. 기본값은 ASCII입니다.</summary>
    public Encoding Encoding
    {
        get => _encoding;
        init => _encoding = value ?? throw new ArgumentNullException(nameof(value));
    }
}
