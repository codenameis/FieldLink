using FieldLink.OpcUa.Subscriptions;

namespace FieldLink.OpcUa.Transport;

internal readonly struct OpcUaMonitoredItemCreationResult
{
    public OpcUaMonitoredItemCreationResult(
        uint clientHandle,
        OpcUaMonitoredItemResult outcome)
    {
        ClientHandle = clientHandle;
        Outcome = outcome;
    }

    public uint ClientHandle { get; }

    public OpcUaMonitoredItemResult Outcome { get; }
}
