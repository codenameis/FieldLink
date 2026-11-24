using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Services;
using FieldLink.OpcUa.Subscriptions;
using Opc.Ua;

namespace FieldLink.OpcUa;

/// <summary>클린룸 OPC UA (IEC 62541) 클라이언트 공개 계약입니다.</summary>
public interface IOpcUaClient : IDisposable, IAsyncDisposable
{
    /// <summary>현재 클라이언트의 로컬 세션 상태 스냅샷을 반환합니다.</summary>
    OpcUaClientState State { get; }

    /// <summary>생성 시 검증·복사된 불변 클라이언트 설정을 반환합니다.</summary>
    OpcUaClientOptions Options { get; }

    /// <summary>Endpoint 탐색, 보안 정책 검증, <c>CreateSession</c> 및 <c>ActivateSession</c>을 완료하고 세션을 엽니다.</summary>
    /// <param name="cancellationToken">호출자 취소 토큰입니다.</param>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>활성 구독과 세션을 서버에서 정리하고 로컬 자원을 해제하여 <see cref="OpcUaClientState.Closed"/> 상태로 전환합니다.</summary>
    /// <param name="cancellationToken">종료 대기 취소 토큰입니다.</param>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>단일 노드의 <c>Value</c> 속성을 읽고 값, <see cref="StatusCode"/>, 타임스탬프를 보존한 <see cref="DataValue"/>를 반환합니다.</summary>
    /// <param name="nodeId">읽을 노드의 <see cref="ExpandedNodeId"/>입니다.</param>
    /// <param name="timeout">전체 제한 시간입니다. <see langword="null"/>이면 기본 작업 제한 시간을 적용합니다.</param>
    /// <param name="cancellationToken">호출자 취소 토큰입니다.</param>
    Task<DataValue> ReadValueAsync(
        ExpandedNodeId nodeId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>여러 노드의 <c>Value</c> 속성을 일괄 읽기하고 입력 순서를 보존한 <see cref="DataValue"/> 목록을 반환합니다.</summary>
    /// <param name="nodeIds">읽을 노드 주소 목록입니다.</param>
    /// <param name="timeout">전체 제한 시간입니다. <see langword="null"/>이면 기본 작업 제한 시간을 적용합니다.</param>
    /// <param name="cancellationToken">호출자 취소 토큰입니다.</param>
    Task<IReadOnlyList<DataValue>> ReadValuesAsync(
        IReadOnlyList<ExpandedNodeId> nodeIds,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>단일 노드에 값을 기록하고 서버 <see cref="StatusCode"/>를 반환합니다. 타임아웃/취소 시 자동 재전송하지 않습니다.</summary>
    /// <param name="request">대상 노드와 명시적 값 요청입니다.</param>
    /// <param name="timeout">전체 제한 시간입니다. <see langword="null"/>이면 기본 작업 제한 시간을 적용합니다.</param>
    /// <param name="cancellationToken">호출자 취소 토큰입니다.</param>
    Task<StatusCode> WriteValueAsync(
        OpcUaWriteRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>여러 노드에 일괄 쓰기를 요청하고 입력 순서대로 개별 <see cref="StatusCode"/>를 반환합니다. 비원자적 부분 성공을 허용하며 자동 재전송하지 않습니다.</summary>
    /// <param name="requests">쓰기 요청 목록입니다.</param>
    /// <param name="timeout">전체 제한 시간입니다. <see langword="null"/>이면 기본 작업 제한 시간을 적용합니다.</param>
    /// <param name="cancellationToken">호출자 취소 토큰입니다.</param>
    Task<IReadOnlyList<StatusCode>> WriteValuesAsync(
        IReadOnlyList<OpcUaWriteRequest> requests,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>지정한 노드의 참조를 <c>Browse</c>/<c>BrowseNext</c>로 끝까지 수집하고 종료·오류 시 <c>ContinuationPoint</c>를 해제합니다.</summary>
    /// <param name="nodeId">탐색 시작 노드 주소입니다.</param>
    /// <param name="options">탐색 방향·마스크·참조 상한 옵션입니다. <see langword="null"/>이면 기본 옵션을 사용합니다.</param>
    /// <param name="cancellationToken">호출자 취소 토큰입니다.</param>
    Task<IReadOnlyList<ReferenceDescription>> BrowseAsync(
        ExpandedNodeId nodeId,
        OpcUaBrowseOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>유한 큐 기반 백프레셔 정책을 사용하는 데이터 변경 구독 핸들을 생성합니다.</summary>
    /// <param name="subscriptionOptions">발행 주기 및 로컬 유한 큐 상한 설정입니다.</param>
    /// <param name="items">감시할 항목 목록입니다.</param>
    /// <param name="timeout">생성 전체 제한 시간입니다. <see langword="null"/>이면 기본 작업 제한 시간을 적용합니다.</param>
    /// <param name="cancellationToken">호출자 취소 토큰입니다.</param>
    Task<OpcUaSubscription> SubscribeAsync(
        OpcUaSubscriptionOptions subscriptionOptions,
        IReadOnlyList<OpcUaMonitoredItemOptions> items,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>서버 객체의 OPC UA 메서드를 호출하고 입력 인수 검증 상태 및 출력 인수를 반환합니다. 자동 재전송하지 않습니다.</summary>
    /// <param name="objectId">메서드를 소유한 객체 노드 주소입니다.</param>
    /// <param name="methodId">호출할 메서드 노드 주소입니다.</param>
    /// <param name="inputArguments">메서드 입력 인수 목록입니다.</param>
    /// <param name="timeout">전체 제한 시간입니다. <see langword="null"/>이면 기본 작업 제한 시간을 적용합니다.</param>
    /// <param name="cancellationToken">호출자 취소 토큰입니다.</param>
    Task<CallMethodResult> CallAsync(
        ExpandedNodeId objectId,
        ExpandedNodeId methodId,
        IReadOnlyList<Variant> inputArguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
