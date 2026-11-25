using System;
using Opc.Ua;

namespace FieldLink.OpcUa.Subscriptions;

/// <summary>구독에 등록할 개별 감시 항목(<c>MonitoredItem</c>) 설정입니다.</summary>
public sealed class OpcUaMonitoredItemOptions
{
    private const int DefaultSamplingIntervalMilliseconds = -1;
    private const int MinimumSamplingIntervalMilliseconds = -1;
    private const uint DefaultQueueSize = 1;
    private const uint MinimumQueueSize = 1;

    /// <summary>애플리케이션이 수신 알림을 원본 요청 항목과 매핑할 고유 키입니다.</summary>
    public string ClientItemKey { get; init; } = string.Empty;

    /// <summary>감시할 대상 노드의 <see cref="ExpandedNodeId"/>입니다.</summary>
    public ExpandedNodeId NodeId { get; init; } = ExpandedNodeId.Null;

    /// <summary>요청할 샘플링 주기(ms)입니다. <c>-1</c>이면 구독의 발행 주기를 따릅니다.</summary>
    public int SamplingIntervalMilliseconds { get; init; } = DefaultSamplingIntervalMilliseconds;

    /// <summary>서버 측 MonitoredItem 큐 크기입니다. 기본값은 1입니다.</summary>
    public uint QueueSize { get; init; } = DefaultQueueSize;

    /// <summary>서버 측 MonitoredItem 큐가 가득 찼을 때 오래된 값을 폐기할지 여부입니다. 기본값은 <see langword="true"/>입니다.</summary>
    public bool DiscardOldest { get; init; } = true;

    internal OpcUaMonitoredItemOptions ValidateAndClone()
    {
        if (string.IsNullOrWhiteSpace(ClientItemKey))
        {
            throw new ArgumentException("ClientItemKey는 비어 있을 수 없습니다.", nameof(ClientItemKey));
        }

        if (NodeId is null || NodeId.IsNull)
        {
            throw new ArgumentException("감시 대상 NodeId는 비어 있을 수 없습니다.", nameof(NodeId));
        }

        if (SamplingIntervalMilliseconds < MinimumSamplingIntervalMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SamplingIntervalMilliseconds),
                "SamplingIntervalMilliseconds는 -1 이상이어야 합니다.");
        }

        if (QueueSize < MinimumQueueSize)
        {
            throw new ArgumentOutOfRangeException(nameof(QueueSize), "서버 큐 크기는 1 이상이어야 합니다.");
        }

        return new OpcUaMonitoredItemOptions
        {
            ClientItemKey = ClientItemKey,
            NodeId = NodeId,
            SamplingIntervalMilliseconds = SamplingIntervalMilliseconds,
            QueueSize = QueueSize,
            DiscardOldest = DiscardOldest
        };
    }
}
