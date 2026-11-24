using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Subscriptions;
using Opc.Ua;
using Opc.Ua.Client;

namespace FieldLink.OpcUa.Transport;

internal interface IOpcUaSessionAdapter : IDisposable
{
    NamespaceTable NamespaceUris { get; }

    OperationLimits? OperationLimits { get; }

    event KeepAliveEventHandler? KeepAlive;

    Task FetchNamespaceTablesAsync(CancellationToken cancellationToken);

    Task FetchOperationLimitsAsync(CancellationToken cancellationToken);

    Task<ReadResponse> ReadAsync(
        uint timeoutHintMilliseconds,
        ReadValueIdCollection nodesToRead,
        CancellationToken cancellationToken);

    Task<WriteResponse> WriteAsync(
        uint timeoutHintMilliseconds,
        WriteValueCollection nodesToWrite,
        CancellationToken cancellationToken);

    Task<BrowseResponse> BrowseAsync(
        uint timeoutHintMilliseconds,
        uint maxReferencesPerNode,
        BrowseDescriptionCollection nodesToBrowse,
        CancellationToken cancellationToken);

    Task<BrowseNextResponse> BrowseNextAsync(
        uint timeoutHintMilliseconds,
        bool releaseContinuationPoints,
        ByteStringCollection continuationPoints,
        CancellationToken cancellationToken);

    Task<CallResponse> CallAsync(
        uint timeoutHintMilliseconds,
        CallMethodRequestCollection methodsToCall,
        CancellationToken cancellationToken);

    IOpcUaSubscriptionAdapter CreateSubscriptionAdapter(
        OpcUaSubscriptionOptions options,
        Action<OpcUaRawDataChangeBatch> onDataChanged);

    Task CloseAsync(int timeoutMilliseconds, CancellationToken cancellationToken);
}
