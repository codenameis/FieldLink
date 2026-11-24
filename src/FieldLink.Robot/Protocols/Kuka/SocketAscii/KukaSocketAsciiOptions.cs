using System;
using System.Text;

namespace FieldLink.Robot.Protocols.Kuka.SocketAscii;

/// <summary>KUKA Socket ASCII TCP 클라이언트에 사용하는 불변 설정입니다.</summary>
public sealed class KukaSocketAsciiOptions
{
    private const int DefaultMaximumResponseBytes = 65536;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _timeout = DefaultTimeout;
    private readonly Encoding _encoding = Encoding.UTF8;
    private readonly int _maximumResponseBytes = DefaultMaximumResponseBytes;

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

    /// <summary>ASCII 명령과 응답 텍스트 인코딩입니다. 기본값은 UTF-8입니다.</summary>
    public Encoding Encoding
    {
        get => _encoding;
        init => _encoding = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>단일 수신에서 허용할 최대 응답 바이트 수입니다. 기본값은 65536입니다.</summary>
    public int MaximumResponseBytes
    {
        get => _maximumResponseBytes;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "최대 응답 크기는 1 이상이어야 합니다.");
            }

            _maximumResponseBytes = value;
        }
    }
}
