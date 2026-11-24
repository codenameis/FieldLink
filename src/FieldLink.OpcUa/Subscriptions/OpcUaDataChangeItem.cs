using Opc.Ua;

namespace FieldLink.OpcUa.Subscriptions;

/// <summary>소유권이 독립적으로 복제되어 수신된 단일 감시 항목 데이터 변경 알림입니다.</summary>
public sealed class OpcUaDataChangeItem
{
    internal OpcUaDataChangeItem(
        string clientItemKey,
        NodeId nodeId,
        DataValue value,
        bool serverOverflow)
    {
        ClientItemKey = clientItemKey;
        NodeId = nodeId;
        Value = value;
        ServerOverflow = serverOverflow;
    }

    /// <summary>요청 시 지정한 항목 식별 키입니다.</summary>
    public string ClientItemKey { get; }

    /// <summary>현재 세션 기준으로 해석된 대상 노드의 <see cref="Opc.Ua.NodeId"/>입니다.</summary>
    public NodeId NodeId { get; }

    /// <summary>내부 배열까지 독립 복제된 <see cref="DataValue"/>(값, <see cref="StatusCode"/>, 타임스탬프 포함)입니다.</summary>
    public DataValue Value { get; }

    /// <summary>서버 측 <c>MonitoredItem</c> 큐 오버플로우(<c>StatusCode.Overflow</c>) InfoBit 설정 여부입니다.</summary>
    public bool ServerOverflow { get; }
}
