using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Internal;
using FieldLink.OpcUa.Services;
using FieldLink.OpcUa.Subscriptions;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa;

/// <summary>보안 정책 강제, 세대 격리 세션 수명, 품질 보존 서비스, Fail-Closed 구독을 제공하는 OPC UA 클라이언트입니다.</summary>
public sealed partial class OpcUaClient : IOpcUaClient
{
    private readonly IOpcUaDiscoveryClient _discoveryClient;
    private readonly OpcUaSessionLifetime _lifetime;

    /// <summary>지정한 옵션으로 새 <see cref="OpcUaClient"/> 인스턴스를 초기화합니다.</summary>
    /// <param name="options">연결·보안·제한 시간 설정입니다.</param>
    public OpcUaClient(OpcUaClientOptions options)
        : this(options, new OpcUaSdkDiscoveryClient(), new OpcUaSdkSessionFactory())
    {
    }

    internal OpcUaClient(
        OpcUaClientOptions options,
        IOpcUaDiscoveryClient discoveryClient,
        IOpcUaSessionFactory sessionFactory)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (discoveryClient is null)
        {
            throw new ArgumentNullException(nameof(discoveryClient));
        }

        if (sessionFactory is null)
        {
            throw new ArgumentNullException(nameof(sessionFactory));
        }

        Options = options.ValidateAndClone();
        _discoveryClient = discoveryClient;
        _lifetime = new OpcUaSessionLifetime(Options, _discoveryClient, sessionFactory);
    }

    /// <inheritdoc/>
    public OpcUaClientState State => _lifetime.State;

    /// <inheritdoc/>
    public OpcUaClientOptions Options { get; }

    /// <inheritdoc/>
    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        return _lifetime.OpenAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        return _lifetime.CloseAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DataValue> ReadValueAsync(
        ExpandedNodeId nodeId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (nodeId is null)
        {
            throw new ArgumentNullException(nameof(nodeId));
        }

        IReadOnlyList<DataValue> results = await ReadValuesAsync(
            new[] { nodeId },
            timeout,
            cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DataValue>> ReadValuesAsync(
        IReadOnlyList<ExpandedNodeId> nodeIds,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (nodeIds is null)
        {
            throw new ArgumentNullException(nameof(nodeIds));
        }

        if (nodeIds.Count == 0)
        {
            throw new ArgumentException("읽을 노드는 최소 1개 이상이어야 합니다.", nameof(nodeIds));
        }

        ExpandedNodeId[] nodeIdSnapshot = SnapshotList(nodeIds);
        DataValue[] results = await _lifetime.ExecuteAsync(
            timeout,
            nameof(ReadValuesAsync),
            (session, _, deadline, operationToken) => OpcUaValueService.ReadValuesAsync(session, deadline, nodeIdSnapshot, operationToken),
            cancellationToken).ConfigureAwait(false);

        return new ReadOnlyCollection<DataValue>(results);
    }

    /// <inheritdoc/>
    public async Task<StatusCode> WriteValueAsync(
        OpcUaWriteRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        IReadOnlyList<StatusCode> results = await WriteValuesAsync(
            new[] { request },
            timeout,
            cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StatusCode>> WriteValuesAsync(
        IReadOnlyList<OpcUaWriteRequest> requests,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        if (requests.Count == 0)
        {
            throw new ArgumentException("쓰기 요청은 최소 1개 이상이어야 합니다.", nameof(requests));
        }

        OpcUaWriteRequest[] requestSnapshot = SnapshotList(requests);
        StatusCode[] results = await _lifetime.ExecuteAsync(
            timeout,
            nameof(WriteValuesAsync),
            (session, _, deadline, operationToken) => OpcUaValueService.WriteValuesAsync(session, deadline, requestSnapshot, operationToken),
            cancellationToken).ConfigureAwait(false);

        return new ReadOnlyCollection<StatusCode>(results);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ReferenceDescription>> BrowseAsync(
        ExpandedNodeId nodeId,
        OpcUaBrowseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (nodeId is null)
        {
            throw new ArgumentNullException(nameof(nodeId));
        }

        OpcUaBrowseOptions validatedOptions = (options ?? new OpcUaBrowseOptions()).ValidateAndClone();
        return _lifetime.ExecuteAsync(
            validatedOptions.Timeout,
            nameof(BrowseAsync),
            (session, _, deadline, operationToken) => OpcUaBrowseService.BrowseAsync(session, deadline, nodeId, validatedOptions, operationToken),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<OpcUaSubscription> SubscribeAsync(
        OpcUaSubscriptionOptions subscriptionOptions,
        IReadOnlyList<OpcUaMonitoredItemOptions> items,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (subscriptionOptions is null)
        {
            throw new ArgumentNullException(nameof(subscriptionOptions));
        }

        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (items.Count == 0)
        {
            throw new ArgumentException("감시 항목은 최소 1개 이상이어야 합니다.", nameof(items));
        }

        OpcUaSubscriptionOptions validatedOptions = subscriptionOptions.ValidateAndClone();
        OpcUaMonitoredItemOptions[] validatedItems = ValidateMonitoredItemList(items);

        return _lifetime.ExecuteAsync(
            timeout,
            nameof(SubscribeAsync),
            (session, capturedGeneration, _, operationToken) =>
                CreateSubscriptionInternalAsync(session, capturedGeneration, validatedOptions, validatedItems, operationToken),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<CallMethodResult> CallAsync(
        ExpandedNodeId objectId,
        ExpandedNodeId methodId,
        IReadOnlyList<Variant> inputArguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (objectId is null)
        {
            throw new ArgumentNullException(nameof(objectId));
        }

        if (methodId is null)
        {
            throw new ArgumentNullException(nameof(methodId));
        }

        if (inputArguments is null)
        {
            throw new ArgumentNullException(nameof(inputArguments));
        }

        OpcUaMethodCallParameters parameters = new(objectId, methodId, SnapshotList(inputArguments));
        return _lifetime.ExecuteAsync(
            timeout,
            nameof(CallAsync),
            (session, _, deadline, operationToken) => OpcUaMethodService.CallAsync(session, deadline, parameters, operationToken),
            cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _lifetime.Dispose();
        _discoveryClient.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (State == OpcUaClientState.Disposed)
        {
            return;
        }

        try
        {
            await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // 동시 Dispose 호출로 이미 폐기된 경우 정상 종료로 간주
        }
        finally
        {
            Dispose();
        }
    }

    private static T[] SnapshotList<T>(IReadOnlyList<T> source)
    {
        T[] snapshot = new T[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            snapshot[i] = source[i];
        }

        return snapshot;
    }
}
