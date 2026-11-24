using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FieldLink.OpcUa.Subscriptions;

/// <summary>단일 <c>PublishResponse</c> 알림에서 추출되어 로컬 유한 큐를 통해 전달되는 데이터 변경 배치입니다.</summary>
public sealed class OpcUaDataChangeBatch
{
    internal OpcUaDataChangeBatch(
        uint sequenceNumber,
        DateTimeOffset publishTime,
        IReadOnlyList<OpcUaDataChangeItem> items)
    {
        SequenceNumber = sequenceNumber;
        PublishTime = publishTime;
        Items = new ReadOnlyCollection<OpcUaDataChangeItem>(new List<OpcUaDataChangeItem>(items));
    }

    /// <summary>서버가 <c>NotificationMessage</c>에 부여한 시퀀스 번호입니다.</summary>
    public uint SequenceNumber { get; }

    /// <summary>서버가 알림 메시지를 발행한 시각입니다.</summary>
    public DateTimeOffset PublishTime { get; }

    /// <summary>배치에 포함된 데이터 변경 항목 목록입니다.</summary>
    public IReadOnlyList<OpcUaDataChangeItem> Items { get; }
}
