using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.OpcUa.Transport;

internal interface IOpcUaSubscriptionAdapter : IDisposable
{
    uint SubscriptionId { get; }

    double RevisedPublishingInterval { get; }

    uint RevisedKeepAliveCount { get; }

    uint RevisedLifetimeCount { get; }

    Task CreateAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<OpcUaMonitoredItemCreationResult>> CreateMonitoredItemsAsync(
        IReadOnlyList<OpcUaResolvedMonitoredItem> items,
        CancellationToken cancellationToken);

    Task DeleteAsync(CancellationToken cancellationToken);
}
