using System;

namespace FieldLink.OpcUa.Subscriptions;

/// <summary>OPC UA 구독 및 로컬 유한 큐(Backpressure) 상한 설정입니다.</summary>
public sealed class OpcUaSubscriptionOptions
{
    private const int DefaultPublishingIntervalMilliseconds = 500;
    private const int MinimumPublishingIntervalMilliseconds = 1;
    private const uint DefaultKeepAliveCount = 10;
    private const uint MinimumKeepAliveCount = 1;
    private const uint DefaultLifetimeCount = 30;
    private const ulong MinimumLifetimeToKeepAliveRatio = 3;
    private const int DefaultMaxQueuedBatches = 64;
    private const int MinimumMaxQueuedBatches = 1;
    private const long DefaultMaxQueuedBytes = 4L * 1024 * 1024;
    private const long MinimumMaxQueuedBytes = 1;

    /// <summary>요청할 서버 발행 주기(ms)입니다. 기본값은 500ms입니다.</summary>
    public int PublishingIntervalMilliseconds { get; init; } = DefaultPublishingIntervalMilliseconds;

    /// <summary>변경 사항이 없을 때 KeepAlive 응답을 보낼 발행 주기 배수입니다. 기본값은 10입니다.</summary>
    public uint KeepAliveCount { get; init; } = DefaultKeepAliveCount;

    /// <summary>클라이언트 응답이 없을 때 서버가 구독을 유지할 발행 주기 배수입니다. IEC 62541-4에 따라 <c>KeepAliveCount * 3</c> 이상이어야 합니다.</summary>
    public uint LifetimeCount { get; init; } = DefaultLifetimeCount;

    /// <summary>단일 Publish 응답당 최대 알림 수입니다. 0이면 서버 기본값을 사용합니다.</summary>
    public uint MaxNotificationsPerPublish { get; init; }

    /// <summary>로컬 유한 큐에 적재할 최대 데이터 변경 배치 개수입니다. 초과 시 Fail-Closed로 종료됩니다.</summary>
    public int MaxQueuedBatches { get; init; } = DefaultMaxQueuedBatches;

    /// <summary>로컬 유한 큐에 적재할 최대 추정 메모리 바이트 수입니다. 초과 시 Fail-Closed로 종료됩니다.</summary>
    public long MaxQueuedBytes { get; init; } = DefaultMaxQueuedBytes;

    internal OpcUaSubscriptionOptions ValidateAndClone()
    {
        if (PublishingIntervalMilliseconds < MinimumPublishingIntervalMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PublishingIntervalMilliseconds),
                "발행 주기는 0ms보다 커야 합니다.");
        }

        if (KeepAliveCount < MinimumKeepAliveCount)
        {
            throw new ArgumentOutOfRangeException(nameof(KeepAliveCount), "KeepAliveCount는 1 이상이어야 합니다.");
        }

        if ((ulong)LifetimeCount < (ulong)KeepAliveCount * MinimumLifetimeToKeepAliveRatio)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LifetimeCount),
                LifetimeCount,
                $"IEC 62541-4 규격에 따라 LifetimeCount는 KeepAliveCount의 {MinimumLifetimeToKeepAliveRatio}배 이상이어야 합니다.");
        }

        if (MaxQueuedBatches < MinimumMaxQueuedBatches)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxQueuedBatches), "최대 큐 배치 수는 1 이상이어야 합니다.");
        }

        if (MaxQueuedBytes < MinimumMaxQueuedBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxQueuedBytes), "최대 큐 바이트 크기는 1 이상이어야 합니다.");
        }

        return new OpcUaSubscriptionOptions
        {
            PublishingIntervalMilliseconds = PublishingIntervalMilliseconds,
            KeepAliveCount = KeepAliveCount,
            LifetimeCount = LifetimeCount,
            MaxNotificationsPerPublish = MaxNotificationsPerPublish,
            MaxQueuedBatches = MaxQueuedBatches,
            MaxQueuedBytes = MaxQueuedBytes
        };
    }
}
