using System;
using System.Collections.Generic;

namespace FieldLink.OpcUa.Transport;

internal sealed class OpcUaRawDataChangeBatch
{
    public OpcUaRawDataChangeBatch(
        uint sequenceNumber,
        DateTimeOffset publishTime,
        IReadOnlyList<OpcUaRawDataChangeItem> items)
    {
        SequenceNumber = sequenceNumber;
        PublishTime = publishTime;
        Items = items;
    }

    public uint SequenceNumber { get; }

    public DateTimeOffset PublishTime { get; }

    public IReadOnlyList<OpcUaRawDataChangeItem> Items { get; }
}
