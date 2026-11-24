using Opc.Ua;

namespace FieldLink.OpcUa.Transport;

internal readonly struct OpcUaResolvedMonitoredItem
{
    public OpcUaResolvedMonitoredItem(
        string clientItemKey,
        NodeId resolvedNodeId,
        int samplingIntervalMilliseconds,
        uint queueSize,
        bool discardOldest)
    {
        ClientItemKey = clientItemKey;
        ResolvedNodeId = resolvedNodeId;
        SamplingIntervalMilliseconds = samplingIntervalMilliseconds;
        QueueSize = queueSize;
        DiscardOldest = discardOldest;
    }

    public string ClientItemKey { get; }

    public NodeId ResolvedNodeId { get; }

    public int SamplingIntervalMilliseconds { get; }

    public uint QueueSize { get; }

    public bool DiscardOldest { get; }
}
