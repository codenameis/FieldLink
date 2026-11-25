using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Internal;
using FieldLink.OpcUa.Subscriptions;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa;

public sealed partial class OpcUaClient
{
    private async Task<OpcUaSubscription> CreateSubscriptionInternalAsync(
        IOpcUaSessionAdapter session,
        long capturedGeneration,
        OpcUaSubscriptionOptions options,
        OpcUaMonitoredItemOptions[] items,
        CancellationToken cancellationToken)
    {
        OpcUaOperationLimitsGuard.ValidateLimit(
            items.Length,
            session.OperationLimits?.MaxMonitoredItemsPerCall,
            "MaxMonitoredItemsPerCall");

        OpcUaResolvedMonitoredItem[] resolvedItems = ResolveMonitoredItems(items, session.NamespaceUris);

        OpcUaSubscription? createdSubscription = null;
        object pendingLock = new();
        List<OpcUaRawDataChangeBatch>? pendingInitialBatches = new();
        long pendingEstimatedBytes = 0;
        bool pendingInitialOverflowTriggered = false;
        IOpcUaSubscriptionAdapter? adapter = null;
        try
        {
            adapter = session.CreateSubscriptionAdapter(
                options,
                batch =>
                {
                    if (batch?.Items is null)
                    {
                        return;
                    }

                    lock (pendingLock)
                    {
                        if (createdSubscription is not null)
                        {
                            // 초기 배치 드레인 이후 모든 콜백을 동일 잠금에서 순서대로 전달해 역전(Out-of-Order)을 막는다.
                            createdSubscription.EnqueueRawBatch(batch);
                            return;
                        }

                        if (pendingInitialBatches is null || pendingInitialOverflowTriggered)
                        {
                            return;
                        }

                        OpcUaRawDataChangeBatch clonedBatch = CloneRawBatch(batch);
                        long batchBytes = OpcUaBatchSnapshot.EstimateRawBatchBytes(clonedBatch);
                        bool exceedsLimits = pendingInitialBatches.Count >= options.MaxQueuedBatches ||
                            batchBytes > options.MaxQueuedBytes - pendingEstimatedBytes;

                        pendingInitialBatches.Add(clonedBatch);
                        pendingEstimatedBytes = OpcUaBatchSnapshot.AddSaturating(pendingEstimatedBytes, batchBytes);
                        if (exceedsLimits)
                        {
                            pendingInitialOverflowTriggered = true;
                        }
                    }
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

            adapter = null;
            if (!_lifetime.RegisterSubscription(subscription, capturedGeneration))
            {
                subscription.TerminateFromParent(OpcUaSubscriptionTerminationReason.ParentClosed, null);
                await CloseUnregisteredSubscriptionSafelyAsync(subscription).ConfigureAwait(false);
                throw _lifetime.CreateStateTransitionException(capturedGeneration);
            }

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

    private static OpcUaRawDataChangeBatch CloneRawBatch(OpcUaRawDataChangeBatch batch)
    {
        List<OpcUaRawDataChangeItem> clonedItems = new(batch.Items.Count);
        for (int i = 0; i < batch.Items.Count; i++)
        {
            OpcUaRawDataChangeItem item = batch.Items[i];
            if (item.Value is null)
            {
                continue;
            }

            clonedItems.Add(new OpcUaRawDataChangeItem(item.ClientHandle, new DataValue(item.Value)));
        }

        return new OpcUaRawDataChangeBatch(batch.SequenceNumber, batch.PublishTime, clonedItems);
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
            // 실패 정리 중 제한 시간 초과 시 로컬 폐기로 진행
        }
        catch (ServiceResultException)
        {
            // 서버 삭제 실패 시에도 로컬 어댑터는 반드시 폐기
        }
        catch (ObjectDisposedException)
        {
            // 어댑터가 이미 해제된 경우 무시
        }
        finally
        {
            adapter.Dispose();
        }
    }

    private async Task CloseUnregisteredSubscriptionSafelyAsync(OpcUaSubscription subscription)
    {
        try
        {
            using CancellationTokenSource closeTimeout = new(Options.CloseTimeout);
            await subscription.CloseAsync(closeTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            subscription.Dispose();
        }
        catch (ServiceResultException)
        {
            subscription.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // 이미 폐기된 구독 무시
        }
    }
}
