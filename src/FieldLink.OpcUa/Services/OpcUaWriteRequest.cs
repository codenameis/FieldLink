using System;
using Opc.Ua;

namespace FieldLink.OpcUa.Services;

/// <summary>단일 노드에 쓸 OPC UA 주소와 명시적 <see cref="Opc.Ua.Variant"/> 값을 표현하는 불변 쓰기 요청입니다.</summary>
/// <remarks>묵시적 숫자 축소 변환이나 구조체 자동 직렬화를 수행하지 않으며, 호출자는 쓰기 작업 완료 시점까지 배열 버퍼를 변경하지 않아야 합니다.</remarks>
public sealed record OpcUaWriteRequest
{
    /// <summary>쓰기 대상 노드의 주소와 값을 지정해 새 요청을 만듭니다.</summary>
    /// <param name="nodeId">대상 노드의 <see cref="ExpandedNodeId"/>입니다(<c>ServerIndex == 0</c> 필수).</param>
    /// <param name="value">서버 노드의 자료형과 일치하는 명시적 <see cref="Opc.Ua.Variant"/> 값입니다.</param>
    public OpcUaWriteRequest(ExpandedNodeId nodeId, Variant value)
    {
        if (nodeId is null)
        {
            throw new ArgumentNullException(nameof(nodeId));
        }

        NodeId = nodeId;
        Value = value;
    }

    /// <summary>대상 노드의 <see cref="ExpandedNodeId"/>를 가져옵니다.</summary>
    public ExpandedNodeId NodeId { get; }

    /// <summary>기록할 OPC UA <see cref="Opc.Ua.Variant"/> 값을 가져옵니다.</summary>
    public Variant Value { get; }
}
