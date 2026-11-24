using Opc.Ua;

namespace FieldLink.OpcUa.Transport;

internal readonly struct OpcUaRawDataChangeItem
{
    public OpcUaRawDataChangeItem(uint clientHandle, DataValue value)
    {
        ClientHandle = clientHandle;
        Value = value;
    }

    public uint ClientHandle { get; }

    public DataValue Value { get; }
}
