using System;
using System.Collections.Generic;
using Opc.Ua;

namespace FieldLink.OpcUa.Internal;

internal static class OpcUaNodeResolver
{
    private const string DefaultParameterName = "nodeId";
    private const uint LocalServerIndex = 0;

    public static NodeId ResolveNodeId(
        ExpandedNodeId? expandedNodeId,
        NamespaceTable namespaceUris,
        string parameterName = DefaultParameterName)
    {
        if (expandedNodeId is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (expandedNodeId.ServerIndex != LocalServerIndex)
        {
            throw new ArgumentException(
                $"타 서버 참조(ServerIndex={expandedNodeId.ServerIndex})는 지원하지 않습니다.",
                parameterName);
        }

        if (expandedNodeId.IsNull)
        {
            throw new ArgumentException("빈 NodeId는 허용되지 않습니다.", parameterName);
        }

        if (!string.IsNullOrEmpty(expandedNodeId.NamespaceUri))
        {
            int index = namespaceUris.GetIndex(expandedNodeId.NamespaceUri);
            if (index < 0)
            {
                throw new ArgumentException(
                    $"현재 세션의 NamespaceArray에 등록되지 않은 NamespaceUri입니다: '{expandedNodeId.NamespaceUri}'.",
                    parameterName);
            }
        }

        NodeId resolved = ExpandedNodeId.ToNodeId(expandedNodeId, namespaceUris);
        if (NodeId.IsNull(resolved))
        {
            throw new ArgumentException("유효한 세션 NodeId로 해석할 수 없습니다.", parameterName);
        }

        return resolved;
    }

    public static NodeId[] ResolveNodeIds(
        IReadOnlyList<ExpandedNodeId> expandedNodeIds,
        NamespaceTable namespaceUris,
        string parameterName)
    {
        NodeId[] resolved = new NodeId[expandedNodeIds.Count];
        for (int i = 0; i < expandedNodeIds.Count; i++)
        {
            resolved[i] = ResolveNodeId(expandedNodeIds[i], namespaceUris, parameterName);
        }

        return resolved;
    }
}
