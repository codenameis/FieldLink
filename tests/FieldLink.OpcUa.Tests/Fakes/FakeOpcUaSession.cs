using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Subscriptions;
using FieldLink.OpcUa.Transport;
using Opc.Ua;
using Opc.Ua.Client;

namespace FieldLink.OpcUa.Tests.Fakes;

internal sealed class FakeDiscoveryClient : IOpcUaDiscoveryClient
{
    public Func<Uri, CancellationToken, Task<IReadOnlyList<EndpointDescription>>>? OnGetEndpoints { get; set; }

    public Task<IReadOnlyList<EndpointDescription>> GetEndpointsAsync(Uri endpointUrl, CancellationToken cancellationToken)
    {
        if (OnGetEndpoints is not null)
        {
            return OnGetEndpoints(endpointUrl, cancellationToken);
        }

        EndpointDescription endpoint = new()
        {
            EndpointUrl = endpointUrl.ToString(),
            TransportProfileUri = Profiles.UaTcpTransport,
            SecurityMode = MessageSecurityMode.None,
            SecurityPolicyUri = SecurityPolicies.None,
            SecurityLevel = 1,
            UserIdentityTokens = new UserTokenPolicyCollection
            {
                new UserTokenPolicy(UserTokenType.Anonymous)
            }
        };

        return Task.FromResult<IReadOnlyList<EndpointDescription>>(new[] { endpoint });
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeSessionFactory : IOpcUaSessionFactory
{
    private int _createCallCount;

    public Func<CancellationToken, Task<IOpcUaSessionAdapter>>? OnCreateSession { get; set; }

    public int CreateCallCount => Volatile.Read(ref _createCallCount);

    public Task<IOpcUaSessionAdapter> CreateSessionAsync(
        ApplicationConfiguration configuration,
        ConfiguredEndpoint endpoint,
        OpcUaClientOptions options,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _createCallCount);
        if (OnCreateSession is not null)
        {
            return OnCreateSession(cancellationToken);
        }

        return Task.FromResult<IOpcUaSessionAdapter>(new FakeSessionAdapter());
    }
}

internal sealed class FakeSessionAdapter : IOpcUaSessionAdapter
{
    public NamespaceTable NamespaceUris { get; set; } = new();

    public OperationLimits? OperationLimits { get; set; }

    public int DisposeCount { get; private set; }

    public int CloseCount { get; private set; }

    public int WriteCallCount { get; private set; }

    public int BrowseNextReleaseCallCount { get; private set; }

    public Func<uint, ReadValueIdCollection, CancellationToken, Task<ReadResponse>>? OnRead { get; set; }

    public Func<uint, WriteValueCollection, CancellationToken, Task<WriteResponse>>? OnWrite { get; set; }

    public Func<uint, uint, BrowseDescriptionCollection, CancellationToken, Task<BrowseResponse>>? OnBrowse { get; set; }

    public Func<uint, bool, ByteStringCollection, CancellationToken, Task<BrowseNextResponse>>? OnBrowseNext { get; set; }

    public Func<uint, CallMethodRequestCollection, CancellationToken, Task<CallResponse>>? OnCall { get; set; }

    public Func<OpcUaSubscriptionOptions, Action<OpcUaRawDataChangeBatch>, IOpcUaSubscriptionAdapter>? OnCreateSubscription { get; set; }

    public event KeepAliveEventHandler? KeepAlive;

    public void RaiseKeepAlive(StatusCode statusCode)
    {
        KeepAlive?.Invoke(null!, new KeepAliveEventArgs(new ServiceResult(statusCode), ServerState.Running, DateTime.UtcNow));
    }

    public Task FetchNamespaceTablesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task FetchOperationLimitsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<ReadResponse> ReadAsync(uint timeoutHintMilliseconds, ReadValueIdCollection nodesToRead, CancellationToken cancellationToken)
    {
        if (OnRead is not null)
        {
            return OnRead(timeoutHintMilliseconds, nodesToRead, cancellationToken);
        }

        return Task.FromResult(new ReadResponse { Results = new DataValueCollection() });
    }

    public Task<WriteResponse> WriteAsync(uint timeoutHintMilliseconds, WriteValueCollection nodesToWrite, CancellationToken cancellationToken)
    {
        WriteCallCount++;
        if (OnWrite is not null)
        {
            return OnWrite(timeoutHintMilliseconds, nodesToWrite, cancellationToken);
        }

        return Task.FromResult(new WriteResponse { Results = new StatusCodeCollection() });
    }

    public Task<BrowseResponse> BrowseAsync(uint timeoutHintMilliseconds, uint maxReferencesPerNode, BrowseDescriptionCollection nodesToBrowse, CancellationToken cancellationToken)
    {
        if (OnBrowse is not null)
        {
            return OnBrowse(timeoutHintMilliseconds, maxReferencesPerNode, nodesToBrowse, cancellationToken);
        }

        return Task.FromResult(new BrowseResponse { Results = new BrowseResultCollection() });
    }

    public Task<BrowseNextResponse> BrowseNextAsync(uint timeoutHintMilliseconds, bool releaseContinuationPoints, ByteStringCollection continuationPoints, CancellationToken cancellationToken)
    {
        if (releaseContinuationPoints)
        {
            BrowseNextReleaseCallCount++;
        }

        if (OnBrowseNext is not null)
        {
            return OnBrowseNext(timeoutHintMilliseconds, releaseContinuationPoints, continuationPoints, cancellationToken);
        }

        return Task.FromResult(new BrowseNextResponse { Results = new BrowseResultCollection { new BrowseResult() } });
    }

    public Task<CallResponse> CallAsync(uint timeoutHintMilliseconds, CallMethodRequestCollection methodsToCall, CancellationToken cancellationToken)
    {
        if (OnCall is not null)
        {
            return OnCall(timeoutHintMilliseconds, methodsToCall, cancellationToken);
        }

        return Task.FromResult(new CallResponse { Results = new CallMethodResultCollection() });
    }

    public IOpcUaSubscriptionAdapter CreateSubscriptionAdapter(OpcUaSubscriptionOptions options, Action<OpcUaRawDataChangeBatch> onDataChanged)
    {
        if (OnCreateSubscription is not null)
        {
            return OnCreateSubscription(options, onDataChanged);
        }

        return new FakeSubscriptionAdapter(onDataChanged);
    }

    public Task CloseAsync(int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        CloseCount++;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        DisposeCount++;
    }
}

internal sealed class FakeSubscriptionAdapter : IOpcUaSubscriptionAdapter
{
    public FakeSubscriptionAdapter(Action<OpcUaRawDataChangeBatch> onDataChanged)
    {
        EmitCallback = onDataChanged;
    }

    public Action<OpcUaRawDataChangeBatch> EmitCallback { get; }

    public uint SubscriptionId { get; set; } = 100;

    public double RevisedPublishingInterval { get; set; } = 100;

    public uint RevisedKeepAliveCount { get; set; } = 5;

    public uint RevisedLifetimeCount { get; set; } = 15;

    public int DeleteCallCount { get; private set; }

    public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<OpcUaMonitoredItemCreationResult>> CreateMonitoredItemsAsync(
        IReadOnlyList<OpcUaResolvedMonitoredItem> items,
        CancellationToken cancellationToken)
    {
        OpcUaMonitoredItemCreationResult[] results = new OpcUaMonitoredItemCreationResult[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            OpcUaMonitoredItemResult outcome = new(
                items[i].ClientItemKey,
                items[i].ResolvedNodeId,
                StatusCodes.Good,
                items[i].SamplingIntervalMilliseconds,
                items[i].QueueSize);
            results[i] = new OpcUaMonitoredItemCreationResult((uint)(i + 1), outcome);
        }

        return Task.FromResult<IReadOnlyList<OpcUaMonitoredItemCreationResult>>(results);
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        DeleteCallCount++;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
