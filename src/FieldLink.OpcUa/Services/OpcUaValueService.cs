using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Internal;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa.Services;

internal static class OpcUaValueService
{
    public static async Task<DataValue[]> ReadValuesAsync(
        IOpcUaSessionAdapter session,
        OpcUaDeadline deadline,
        IReadOnlyList<ExpandedNodeId> nodeIds,
        CancellationToken cancellationToken)
    {
        if (nodeIds is null)
        {
            throw new ArgumentNullException(nameof(nodeIds));
        }

        if (nodeIds.Count == 0)
        {
            throw new ArgumentException("읽을 노드는 최소 1개 이상이어야 합니다.", nameof(nodeIds));
        }

        NodeId[] resolvedIds = OpcUaNodeResolver.ResolveNodeIds(nodeIds, session.NamespaceUris, nameof(nodeIds));
        OpcUaOperationLimitsGuard.ValidateLimit(resolvedIds.Length, session.OperationLimits?.MaxNodesPerRead, "MaxNodesPerRead");

        ReadValueIdCollection readCollection = new(resolvedIds.Length);
        for (int i = 0; i < resolvedIds.Length; i++)
        {
            readCollection.Add(new ReadValueId
            {
                NodeId = resolvedIds[i],
                AttributeId = Attributes.Value
            });
        }

        uint timeoutHint = deadline.GetOperationTimeoutHintMilliseconds(cancellationToken, nameof(ReadValuesAsync));
        ReadResponse response = await session
            .ReadAsync(timeoutHint, readCollection, cancellationToken)
            .ConfigureAwait(false);

        if (response.Results is null || response.Results.Count != resolvedIds.Length)
        {
            int actualCount = response.Results?.Count ?? 0;
            throw new ServiceResultException(
                StatusCodes.BadUnexpectedError,
                $"서버 Read 응답 항목 수({actualCount})가 요청 수({resolvedIds.Length})와 일치하지 않습니다.");
        }

        DataValue[] results = new DataValue[response.Results.Count];
        for (int i = 0; i < response.Results.Count; i++)
        {
            DataValue? value = response.Results[i] ?? throw new ServiceResultException(
                StatusCodes.BadUnexpectedError,
                $"서버 Read 응답 항목 인덱스 {i}가 null입니다.");
            results[i] = new DataValue(value);
        }

        return results;
    }

    public static async Task<StatusCode[]> WriteValuesAsync(
        IOpcUaSessionAdapter session,
        OpcUaDeadline deadline,
        IReadOnlyList<OpcUaWriteRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        if (requests.Count == 0)
        {
            throw new ArgumentException("쓰기 요청은 최소 1개 이상이어야 합니다.", nameof(requests));
        }

        WriteValueCollection writeCollection = BuildWriteCollection(requests, session.NamespaceUris);
        OpcUaOperationLimitsGuard.ValidateLimit(writeCollection.Count, session.OperationLimits?.MaxNodesPerWrite, "MaxNodesPerWrite");

        uint timeoutHint = deadline.GetOperationTimeoutHintMilliseconds(cancellationToken, nameof(WriteValuesAsync));

        // 쓰기 요청은 타임아웃·네트워크 단절 시 서버 반영 여부가 불확실하므로 절대 자동 재전송하지 않는다.
        WriteResponse response = await session
            .WriteAsync(timeoutHint, writeCollection, cancellationToken)
            .ConfigureAwait(false);

        if (response.Results is null || response.Results.Count != requests.Count)
        {
            int actualCount = response.Results?.Count ?? 0;
            throw new ServiceResultException(
                StatusCodes.BadUnexpectedError,
                $"서버 Write 응답 항목 수({actualCount})가 요청 수({requests.Count})와 일치하지 않습니다.");
        }

        StatusCode[] results = new StatusCode[response.Results.Count];
        for (int i = 0; i < response.Results.Count; i++)
        {
            results[i] = response.Results[i];
        }

        return results;
    }

    private static WriteValueCollection BuildWriteCollection(
        IReadOnlyList<OpcUaWriteRequest> requests,
        NamespaceTable namespaceUris)
    {
        WriteValueCollection writeCollection = new(requests.Count);
        for (int i = 0; i < requests.Count; i++)
        {
            OpcUaWriteRequest request = requests[i]
                ?? throw new ArgumentException($"요청 목록의 인덱스 {i} 항목이 null입니다.", nameof(requests));

            NodeId resolvedId = OpcUaNodeResolver.ResolveNodeId(request.NodeId, namespaceUris, nameof(requests));
            writeCollection.Add(new WriteValue
            {
                NodeId = resolvedId,
                AttributeId = Attributes.Value,
                Value = new DataValue(request.Value)
            });
        }

        return writeCollection;
    }
}
