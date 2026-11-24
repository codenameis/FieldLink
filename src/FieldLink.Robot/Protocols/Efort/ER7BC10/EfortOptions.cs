using System;

namespace FieldLink.Robot.Protocols.Efort.ER7BC10;

/// <summary>EFORT ER7B-C10 프로토콜 통신에 사용하는 불변 설정입니다.</summary>
public sealed class EfortOptions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _timeout = DefaultTimeout;

    /// <summary>패킷 정렬 버전을 지정합니다. 기본값은 현행 <see cref="EfortProtocolVersion.V2Aligned"/>입니다.</summary>
    public EfortProtocolVersion Version { get; init; } = EfortProtocolVersion.V2Aligned;

    /// <summary>단일 상태 스냅샷 조회 제한 시간입니다. 기본값은 5초입니다.</summary>
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

    internal IEfortPacketLayout GetLayout() =>
        Version == EfortProtocolVersion.V2Aligned
            ? EfortV2AlignedLayout.Instance
            : EfortV1UnalignedLayout.Instance;
}
