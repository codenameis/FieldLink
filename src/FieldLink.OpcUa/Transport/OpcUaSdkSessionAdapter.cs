using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Subscriptions;
using Opc.Ua;
using Opc.Ua.Client;

namespace FieldLink.OpcUa.Transport;

internal sealed class OpcUaSdkSessionAdapter : IOpcUaSessionAdapter
{
    private readonly ISession _session;

    public OpcUaSdkSessionAdapter(ISession session)
    {
        _session = session;
    }

    public NamespaceTable NamespaceUris => _session.NamespaceUris;

    public OperationLimits? OperationLimits => _session.OperationLimits;

    public event KeepAliveEventHandler? KeepAlive
    {
        add => _session.KeepAlive += value;
        remove => _session.KeepAlive -= value;
    }

    public Task FetchNamespaceTablesAsync(CancellationToken cancellationToken)
    {
        return _session.FetchNamespaceTablesAsync(cancellationToken);
    }

    public Task FetchOperationLimitsAsync(CancellationToken cancellationToken)
    {
        if (_session is Session concreteSession)
        {
            return concreteSession.FetchOperationLimitsAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }

    public Task<ReadResponse> ReadAsync(
        uint timeoutHintMilliseconds,
        ReadValueIdCollection nodesToRead,
        CancellationToken cancellationToken)
    {
        RequestHeader header = new() { TimeoutHint = timeoutHintMilliseconds };
        return _session.ReadAsync(header, maxAge: 0, TimestampsToReturn.Both, nodesToRead, cancellationToken);
    }

    public Task<WriteResponse> WriteAsync(
        uint timeoutHintMilliseconds,
        WriteValueCollection nodesToWrite,
        CancellationToken cancellationToken)
    {
        RequestHeader header = new() { TimeoutHint = timeoutHintMilliseconds };
        return _session.WriteAsync(header, nodesToWrite, cancellationToken);
    }

    public Task<BrowseResponse> BrowseAsync(
        uint timeoutHintMilliseconds,
        uint maxReferencesPerNode,
        BrowseDescriptionCollection nodesToBrowse,
        CancellationToken cancellationToken)
    {
        RequestHeader header = new() { TimeoutHint = timeoutHintMilliseconds };
        return _session.BrowseAsync(header, view: null, maxReferencesPerNode, nodesToBrowse, cancellationToken);
    }

    public Task<BrowseNextResponse> BrowseNextAsync(
        uint timeoutHintMilliseconds,
        bool releaseContinuationPoints,
        ByteStringCollection continuationPoints,
        CancellationToken cancellationToken)
    {
        RequestHeader header = new() { TimeoutHint = timeoutHintMilliseconds };
        return _session.BrowseNextAsync(header, releaseContinuationPoints, continuationPoints, cancellationToken);
    }

    public Task<CallResponse> CallAsync(
        uint timeoutHintMilliseconds,
        CallMethodRequestCollection methodsToCall,
        CancellationToken cancellationToken)
    {
        RequestHeader header = new() { TimeoutHint = timeoutHintMilliseconds };
        return _session.CallAsync(header, methodsToCall, cancellationToken);
    }

    public IOpcUaSubscriptionAdapter CreateSubscriptionAdapter(
        OpcUaSubscriptionOptions options,
        Action<OpcUaRawDataChangeBatch> onDataChanged)
    {
        return new OpcUaSdkSubscriptionAdapter(_session, options, onDataChanged);
    }

    public async Task CloseAsync(int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        await _session.CloseAsync(timeoutMilliseconds, closeChannel: true, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
