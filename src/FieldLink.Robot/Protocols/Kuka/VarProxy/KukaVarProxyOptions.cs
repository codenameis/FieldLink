using System;
using System.Text;

namespace FieldLink.Robot.Protocols.Kuka.VarProxy;

/// <summary>KUKAVARPROXY Binary TCP 클라이언트의 불변 설정입니다.</summary>
public sealed class KukaVarProxyOptions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _timeout = DefaultTimeout;
    private readonly Encoding _encoding = Encoding.ASCII;

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

    /// <summary>변수 이름과 값 문자열 인코딩입니다. 기본값은 ASCII입니다.</summary>
    public Encoding Encoding
    {
        get => _encoding;
        init => _encoding = value ?? throw new ArgumentNullException(nameof(value));
    }
}
