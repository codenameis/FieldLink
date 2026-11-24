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
public sealed class OpcUaClient : IOpcUaClient
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

        DataValue[] results = await _lifetime.ExecuteAsync(
            timeout,
            nameof(ReadValuesAsync),
            (session, deadline, operationToken) => OpcUaValueService.ReadValuesAsync(session, deadline, nodeIds, operationToken),
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

        StatusCode[] results = await _lifetime.ExecuteAsync(
            timeout,
            nameof(WriteValuesAsync),
            (session, deadline, operationToken) => OpcUaValueService.WriteValuesAsync(session, deadline, requests, operationToken),
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
            (session, deadline, operationToken) => OpcUaBrowseService.BrowseAsync(session, deadline, nodeId, validatedOptions, operationToken),
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
            (session, _, operationToken) => CreateSubscriptionInternalAsync(session, validatedOptions, validatedItems, operationToken),
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

        OpcUaMethodCallParameters parameters = new(objectId, methodId, inputArguments);
        return _lifetime.ExecuteAsync(
            timeout,
            nameof(CallAsync),
            (session, deadline, operationToken) => OpcUaMethodService.CallAsync(session, deadline, parameters, operationToken),
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
        }
        finally
        {
            Dispose();
        }
    }

    private async Task<OpcUaSubscription> CreateSubscriptionInternalAsync(
        IOpcUaSessionAdapter session,
        OpcUaSubscriptionOptions options,
        OpcUaMonitoredItemOptions[] items,
        CancellationToken cancellationToken)
    {
        long capturedGeneration = _lifetime.CurrentGeneration;
        OpcUaResolvedMonitoredItem[] resolvedItems = ResolveMonitoredItems(items, session.NamespaceUris);

        OpcUaSubscription? createdSubscription = null;
        object pendingLock = new();
        List<OpcUaRawDataChangeBatch>? pendingInitialBatches = new();
        IOpcUaSubscriptionAdapter? adapter = null;
        try
        {
            adapter = session.CreateSubscriptionAdapter(
                options,
                batch =>
                {
                    OpcUaSubscription? target;
                    lock (pendingLock)
                    {
                        target = createdSubscription;
                        if (target is null)
                        {
                            pendingInitialBatches?.Add(batch);
                            return;
                        }
                    }

                    target.EnqueueRawBatch(batch);
                });

            await adapter.CreateAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<OpcUaMonitoredItemCreationResult> creationResults = await adapter
                .CreateMonitoredItemsAsync(resolvedItems, cancellationToken)
                .ConfigureAwait(false);

            EnsureAtLeastOneMonitoredItemSucceeded(creationResults);

            OpcUaSubscription subscription = new(
                options,
                adapter,
                _lifetime,
                capturedGeneration,
                Options.CloseTimeout,
                creationResults);

            lock (pendingLock)
            {
                if (pendingInitialBatches is not null)
                {
                    for (int i = 0; i < pendingInitialBatches.Count; i++)
                    {
                        subscription.EnqueueRawBatch(pendingInitialBatches[i]);
                    }

                    pendingInitialBatches = null;
                }

                createdSubscription = subscription;
            }

            if (!_lifetime.RegisterSubscription(subscription, capturedGeneration))
            {
                subscription.TerminateFromParent(OpcUaSubscriptionTerminationReason.ParentClosed, null);
                throw _lifetime.CreateStateTransitionException(capturedGeneration);
            }

            adapter = null;
            return subscription;
        }
        finally
        {
            if (adapter is not null)
            {
                await CleanupFailedSubscriptionAdapterAsync(adapter).ConfigureAwait(false);
            }
        }
    }

    private static OpcUaMonitoredItemOptions[] ValidateMonitoredItemList(IReadOnlyList<OpcUaMonitoredItemOptions> items)
    {
        OpcUaMonitoredItemOptions[] validated = new OpcUaMonitoredItemOptions[items.Count];
        HashSet<string> seenKeys = new(StringComparer.Ordinal);
        for (int i = 0; i < items.Count; i++)
        {
            OpcUaMonitoredItemOptions item = items[i]
                ?? throw new ArgumentException($"감시 항목 목록의 인덱스 {i}가 null입니다.", nameof(items));

            OpcUaMonitoredItemOptions normalized = item.ValidateAndClone();
            if (!seenKeys.Add(normalized.ClientItemKey))
            {
                throw new ArgumentException($"중복된 ClientItemKey입니다: '{normalized.ClientItemKey}'.", nameof(items));
            }

            validated[i] = normalized;
        }

        return validated;
    }

    private static OpcUaResolvedMonitoredItem[] ResolveMonitoredItems(
        OpcUaMonitoredItemOptions[] items,
        NamespaceTable namespaceUris)
    {
        OpcUaResolvedMonitoredItem[] resolved = new OpcUaResolvedMonitoredItem[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            OpcUaMonitoredItemOptions item = items[i];
            NodeId nodeId = OpcUaNodeResolver.ResolveNodeId(item.NodeId, namespaceUris, nameof(items));
            resolved[i] = new OpcUaResolvedMonitoredItem(
                item.ClientItemKey,
                nodeId,
                item.SamplingIntervalMilliseconds,
                item.QueueSize,
                item.DiscardOldest);
        }

        return resolved;
    }

    private static void EnsureAtLeastOneMonitoredItemSucceeded(IReadOnlyList<OpcUaMonitoredItemCreationResult> results)
    {
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].Outcome.IsSuccess)
            {
                return;
            }
        }

        StatusCode firstFailure = results.Count > 0 ? results[0].Outcome.StatusCode : StatusCodes.BadUnexpectedError;
        throw new ServiceResultException(
            firstFailure,
            $"모든 감시 항목({results.Count}건) 생성이 실패하여 서버 구독을 정리했습니다. 첫 실패 코드: {firstFailure}");
    }

    private async Task CleanupFailedSubscriptionAdapterAsync(IOpcUaSubscriptionAdapter adapter)
    {
        try
        {
            using CancellationTokenSource deleteTimeout = new(Options.CloseTimeout);
            await adapter.DeleteAsync(deleteTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ServiceResultException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            adapter.Dispose();
        }
    }
}
