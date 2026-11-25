using System;
using FieldLink.OpcUa.Internal;
using Opc.Ua;

namespace FieldLink.OpcUa.Services;

/// <summary>OPC UA 노드 탐색(<c>Browse</c>/<c>BrowseNext</c>) 동작과 안전 상한을 지정하는 옵션입니다.</summary>
public sealed class OpcUaBrowseOptions
{
    private const int DefaultMaxTotalReferences = 10_000;
    private const int MinimumTotalReferences = 1;

    /// <summary>탐색 방향입니다. 기본값은 <see cref="Opc.Ua.BrowseDirection.Forward"/>입니다.</summary>
    public BrowseDirection BrowseDirection { get; init; } = BrowseDirection.Forward;

    /// <summary>따라갈 참조 유형입니다. 기본값은 <see cref="ReferenceTypeIds.HierarchicalReferences"/>입니다.</summary>
    public NodeId ReferenceTypeId { get; init; } = ReferenceTypeIds.HierarchicalReferences;

    /// <summary>지정한 참조 유형의 하위 타입을 포함할지 여부입니다. 기본값은 <see langword="true"/>입니다.</summary>
    public bool IncludeSubtypes { get; init; } = true;

    /// <summary>필터링할 <see cref="NodeClass"/> 비트마스크입니다. 0이면 모든 노드 클래스를 포함합니다.</summary>
    public uint NodeClassMask { get; init; }

    /// <summary>단일 <c>Browse</c>/<c>BrowseNext</c> 페이지당 서버에 요청할 최대 참조 수입니다. 0이면 서버 기본값을 사용합니다.</summary>
    public uint MaxReferencesPerPage { get; init; }

    /// <summary>전체 페이지를 합산하여 수집할 최대 참조 수 상한입니다. 초과 시 잘린 결과를 성공으로 반환하지 않고 명시적으로 실패합니다.</summary>
    public int MaxTotalReferences { get; init; } = DefaultMaxTotalReferences;

    /// <summary><c>Browse</c> 및 모든 <c>BrowseNext</c> 페이지를 합산한 전체 제한 시간입니다. <see langword="null"/>이면 클라이언트 기본 작업 제한 시간을 적용합니다.</summary>
    public TimeSpan? Timeout { get; init; }

    internal OpcUaBrowseOptions ValidateAndClone()
    {
        if (!Enum.IsDefined(typeof(BrowseDirection), BrowseDirection))
        {
            throw new ArgumentOutOfRangeException(nameof(BrowseDirection), "지원하지 않는 탐색 방향입니다.");
        }

        if (ReferenceTypeId is null || NodeId.IsNull(ReferenceTypeId))
        {
            throw new ArgumentException("ReferenceTypeId는 비어 있을 수 없습니다.", nameof(ReferenceTypeId));
        }

        if (MaxTotalReferences < MinimumTotalReferences)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxTotalReferences),
                "MaxTotalReferences는 1 이상이어야 합니다.");
        }

        if (Timeout.HasValue)
        {
            OpcUaDeadline.ValidatePositiveTimeout(
                Timeout.Value,
                nameof(Timeout),
                "Timeout은 0보다 크고 int.MaxValue 밀리초 이하이어야 합니다.");
        }

        return new OpcUaBrowseOptions
        {
            BrowseDirection = BrowseDirection,
            ReferenceTypeId = ReferenceTypeId,
            IncludeSubtypes = IncludeSubtypes,
            NodeClassMask = NodeClassMask,
            MaxReferencesPerPage = MaxReferencesPerPage,
            MaxTotalReferences = MaxTotalReferences,
            Timeout = Timeout
        };
    }
}
