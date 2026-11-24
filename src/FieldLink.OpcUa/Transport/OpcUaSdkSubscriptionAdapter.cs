using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Subscriptions;
using Opc.Ua;
using Opc.Ua.Client;

namespace FieldLink.OpcUa.Transport;

internal sealed class OpcUaSdkSubscriptionAdapter : IOpcUaSubscriptionAdapter
{
    private readonly ISession _session;
    private readonly Subscription _subscription;
    private readonly Action<OpcUaRawDataChangeBatch> _onDataChanged;
    private int _disposed;

    public OpcUaSdkSubscriptionAdapter(
        ISession session,
        OpcUaSubscriptionOptions options,
        Action<OpcUaRawDataChangeBatch> onDataChanged)
    {
        _session = session;
        _onDataChanged = onDataChanged;
        _subscription = new Subscription(session.DefaultSubscription)
        {
            PublishingInterval = options.PublishingIntervalMilliseconds,
            KeepAliveCount = options.KeepAliveCount,
            LifetimeCount = options.LifetimeCount,
            MaxNotificationsPerPublish = options.MaxNotificationsPerPublish,
            PublishingEnabled = true,
            SequentialPublishing = true,
            FastDataChangeCallback = OnFastDataChange
        };
    }

    public uint SubscriptionId => _subscription.Id;

    public double RevisedPublishingInterval => _subscription.CurrentPublishingInterval;

    public uint RevisedKeepAliveCount => _subscription.CurrentKeepAliveCount;

    public uint RevisedLifetimeCount => _subscription.CurrentLifetimeCount;

    public async Task CreateAsync(CancellationToken cancellationToken)
    {
        _session.AddSubscription(_subscription);
        await _subscription.CreateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OpcUaMonitoredItemCreationResult>> CreateMonitoredItemsAsync(
        IReadOnlyList<OpcUaResolvedMonitoredItem> items,
        CancellationToken cancellationToken)
    {
        List<MonitoredItem> monitoredItems = new(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            OpcUaResolvedMonitoredItem item = items[i];
            MonitoredItem sdkItem = new(_subscription.DefaultItem)
            {
                DisplayName = item.ClientItemKey,
                StartNodeId = item.ResolvedNodeId,
                AttributeId = Attributes.Value,
                SamplingInterval = item.SamplingIntervalMilliseconds,
                QueueSize = item.QueueSize,
                DiscardOldest = item.DiscardOldest,
                MonitoringMode = MonitoringMode.Reporting,
                Handle = item.ClientItemKey
            };
            monitoredItems.Add(sdkItem);
        }

        _subscription.AddItems(monitoredItems);
        await _subscription.CreateItemsAsync(cancellationToken).ConfigureAwait(false);

        OpcUaMonitoredItemCreationResult[] results = new OpcUaMonitoredItemCreationResult[monitoredItems.Count];
        for (int i = 0; i < monitoredItems.Count; i++)
        {
            MonitoredItem sdkItem = monitoredItems[i];
            MonitoredItemStatus status = sdkItem.Status;
            StatusCode statusCode = status.Error?.StatusCode ?? StatusCodes.Good;
            OpcUaMonitoredItemResult outcome = new(
                items[i].ClientItemKey,
                items[i].ResolvedNodeId,
                statusCode,
                status.SamplingInterval,
                status.QueueSize);
            results[i] = new OpcUaMonitoredItemCreationResult(sdkItem.ClientHandle, outcome);
        }

        return results;
    }

    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _subscription.DeleteAsync(silent: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await _session.RemoveSubscriptionAsync(_subscription, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceResultException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _subscription.FastDataChangeCallback = null;
        _subscription.Dispose();
    }

    private void OnFastDataChange(
        Subscription subscription,
        DataChangeNotification notification,
        IList<string> stringTable)
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            notification?.MonitoredItems is not { Count: > 0 } monitoredItems)
        {
            return;
        }

        OpcUaRawDataChangeItem[] items = new OpcUaRawDataChangeItem[monitoredItems.Count];
        for (int i = 0; i < monitoredItems.Count; i++)
        {
            MonitoredItemNotification itemNotification = monitoredItems[i];
            items[i] = new OpcUaRawDataChangeItem(itemNotification.ClientHandle, itemNotification.Value);
        }

        DateTimeOffset publishTime = ResolvePublishTimeUtc(notification.PublishTime);
        _onDataChanged(new OpcUaRawDataChangeBatch(notification.SequenceNumber, publishTime, items));
    }

    private static DateTimeOffset ResolvePublishTimeUtc(DateTime rawPublishTime)
    {
        if (rawPublishTime == DateTime.MinValue)
        {
            return DateTimeOffset.UtcNow;
        }

        DateTime utcTime = rawPublishTime.Kind == DateTimeKind.Local
            ? rawPublishTime.ToUniversalTime()
            : DateTime.SpecifyKind(rawPublishTime, DateTimeKind.Utc);
        return new DateTimeOffset(utcTime);
    }
}
