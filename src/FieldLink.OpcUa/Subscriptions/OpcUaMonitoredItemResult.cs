using Opc.Ua;

namespace FieldLink.OpcUa.Subscriptions;

/// <summary>서버에 <c>CreateMonitoredItems</c>를 요청한 뒤 개별 감시 항목에 대해 서버가 조정·응답한 생성 결과입니다.</summary>
public sealed class OpcUaMonitoredItemResult
{
    internal OpcUaMonitoredItemResult(
        string clientItemKey,
        NodeId resolvedNodeId,
        StatusCode statusCode,
        double revisedSamplingInterval,
        uint revisedQueueSize)
    {
        ClientItemKey = clientItemKey;
        ResolvedNodeId = resolvedNodeId;
        StatusCode = statusCode;
        RevisedSamplingInterval = revisedSamplingInterval;
        RevisedQueueSize = revisedQueueSize;
    }

    /// <summary>요청 시 지정한 애플리케이션 항목 식별 키입니다.</summary>
    public string ClientItemKey { get; }

    /// <summary>현재 세션의 <c>NamespaceArray</c> 기준으로 해석된 세션 <see cref="NodeId"/>입니다.</summary>
    public NodeId ResolvedNodeId { get; }

    /// <summary>항목 생성에 대한 서버 응답 <see cref="StatusCode"/>입니다.</summary>
    public StatusCode StatusCode { get; }

    /// <summary>서버가 조정해 확정한 샘플링 주기(ms)입니다.</summary>
    public double RevisedSamplingInterval { get; }

    /// <summary>서버가 조정해 확정한 서버 측 큐 크기입니다.</summary>
    public uint RevisedQueueSize { get; }

    /// <summary>항목 생성이 성공(<c>Good</c>)했는지 여부를 반환합니다.</summary>
    public bool IsSuccess => StatusCode.IsGood(StatusCode);
}
