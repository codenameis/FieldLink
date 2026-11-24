using System;

namespace FieldLink.OpcUa.Subscriptions;

/// <summary>OPC UA 구독 및 로컬 유한 큐(Backpressure) 상한 설정입니다.</summary>
public sealed class OpcUaSubscriptionOptions
{
    /// <summary>요청할 서버 발행 주기(ms)입니다. 기본값은 500ms입니다.</summary>
    public int PublishingIntervalMilliseconds { get; init; } = 500;

    /// <summary>변경 사항이 없을 때 KeepAlive 응답을 보낼 발행 주기 배수입니다. 기본값은 10입니다.</summary>
    public uint KeepAliveCount { get; init; } = 10;

    /// <summary>클라이언트 응답이 없을 때 서버가 구독을 유지할 발행 주기 배수입니다. <c>KeepAliveCount * 3</c> 이상을 권장합니다.</summary>
    public uint LifetimeCount { get; init; } = 30;

    /// <summary>단일 Publish 응답당 최대 알림 수입니다. 0이면 서버 기본값을 사용합니다.</summary>
    public uint MaxNotificationsPerPublish { get; init; }

    /// <summary>로컬 유한 큐에 적재할 최대 데이터 변경 배치 개수입니다. 초과 시 Fail-Closed로 종료됩니다.</summary>
    public int MaxQueuedBatches { get; init; } = 64;

    /// <summary>로컬 유한 큐에 적재할 최대 추정 메모리 바이트 수입니다. 초과 시 Fail-Closed로 종료됩니다.</summary>
    public long MaxQueuedBytes { get; init; } = 4 * 1024 * 1024;

    internal OpcUaSubscriptionOptions ValidateAndClone()
    {
        if (PublishingIntervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PublishingIntervalMilliseconds),
                "발행 주기는 0ms보다 커야 합니다.");
        }

        if (KeepAliveCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(KeepAliveCount), "KeepAliveCount는 1 이상이어야 합니다.");
        }

        if (LifetimeCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LifetimeCount), "LifetimeCount는 1 이상이어야 합니다.");
        }

        if (MaxQueuedBatches < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxQueuedBatches), "최대 큐 배치 수는 1 이상이어야 합니다.");
        }

        if (MaxQueuedBytes < 1)
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
