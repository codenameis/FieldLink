using System;
using System.Net;

namespace FieldLink.Robot.Protocols.Hyundai.OnLTrack;

/// <summary>Hyundai Online Tracking UDP 수신 서버의 불변 설정입니다.</summary>
public sealed class HyundaiOnLTrackOptions
{
    private const int MinimumPort = 0;
    private const int MaximumPort = 65535;
    private const int DefaultPort = 7127;
    private const int DefaultTimeoutSeconds = 5;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);

    private readonly int _localPort = DefaultPort;
    private readonly IPAddress _localAddress = IPAddress.Any;
    private readonly TimeSpan _timeout = DefaultTimeout;

    /// <summary>로봇 JOB의 <c>PORT</c>와 일치시킬 로컬 UDP 바인딩 포트입니다. 기본값은 7127입니다.</summary>
    public int LocalPort
    {
        get => _localPort;
        init
        {
            if (value is < MinimumPort or > MaximumPort)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"로컬 포트 번호는 {MinimumPort}에서 {MaximumPort} 사이여야 합니다.");
            }

            _localPort = value;
        }
    }

    /// <summary>바인딩할 로컬 IP 주소입니다. 기본값은 <see cref="IPAddress.Any"/>입니다.</summary>
    public IPAddress LocalAddress
    {
        get => _localAddress;
        init => _localAddress = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>송신 작업 제한 시간입니다. 기본값은 5초입니다.</summary>
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
}
