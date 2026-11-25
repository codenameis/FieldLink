using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Internal;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa.Subscriptions;

public sealed partial class OpcUaSubscription
{
    internal void EnqueueRawBatch(OpcUaRawDataChangeBatch rawBatch)
    {
        if (_lifetime.CurrentGeneration != _capturedGeneration)
        {
            return;
        }

        if (!OpcUaBatchSnapshot.TryBuild(
                rawBatch,
                _itemsByClientHandle,
                out OpcUaDataChangeBatch? batch,
                out long estimatedBytes) ||
            batch is null)
        {
            return;
        }

        bool overflowTriggered = EnqueueOrFaultLocked(batch, estimatedBytes, out Task? overflowCloseTask);
        if (overflowTriggered && overflowCloseTask is not null)
        {
            _lifetime.UnregisterSubscription(this);
            OpcUaDeadline.ObserveOrphanedFault(overflowCloseTask);
        }
    }

    internal void TerminateFromParent(OpcUaSubscriptionTerminationReason reason, Exception? cause)
    {
        lock (_queueLock)
        {
            if (_terminationReason != OpcUaSubscriptionTerminationReason.None)
            {
                return;
            }

            bool clearQueue = reason == OpcUaSubscriptionTerminationReason.ParentFaulted;
            TransitionLocked(reason, cause, clearQueue);
        }
    }

    private bool TryDequeueOrCreateWaiter(
        CancellationToken cancellationToken,
        out OpcUaDataChangeBatch? batch,
        out Task<bool>? waitTask)
    {
        lock (_queueLock)
        {
            if (_terminationReason == OpcUaSubscriptionTerminationReason.Disposed)
            {
                throw new ObjectDisposedException(nameof(OpcUaSubscription));
            }

            if (_consumerInFlight)
            {
                throw new InvalidOperationException("OpcUaSubscription.ReadNextAsync는 단일 소비자만 동시에 호출할 수 있습니다.");
            }

            if (_terminationReason is OpcUaSubscriptionTerminationReason.QueueOverflow or OpcUaSubscriptionTerminationReason.ParentFaulted)
            {
                throw _terminationException ?? new InvalidOperationException("구독이 오류 상태로 종료되었습니다.");
            }

            if (_queue.Count > 0)
            {
                QueuedBatch entry = _queue.Dequeue();
                _queuedBytes -= entry.EstimatedBytes;
                batch = entry.Batch;
                waitTask = null;
                return true;
            }

            if (_terminationReason is OpcUaSubscriptionTerminationReason.Closed or OpcUaSubscriptionTerminationReason.ParentClosed)
            {
                batch = null;
                waitTask = null;
                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _consumerInFlight = true;
            _waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            batch = null;
            waitTask = _waiter.Task;
            return false;
        }
    }

    private bool EnqueueOrFaultLocked(
        OpcUaDataChangeBatch batch,
        long estimatedBytes,
        out Task? overflowCloseTask)
    {
        lock (_queueLock)
        {
            if (_terminationReason != OpcUaSubscriptionTerminationReason.None ||
                _lifetime.CurrentGeneration != _capturedGeneration)
            {
                overflowCloseTask = null;
                return false;
            }

            if (_queue.Count < _options.MaxQueuedBatches &&
                estimatedBytes <= _options.MaxQueuedBytes - _queuedBytes)
            {
                _queue.Enqueue(new QueuedBatch(batch, estimatedBytes));
                _queuedBytes += estimatedBytes;
                _waiter?.TrySetResult(true);
                overflowCloseTask = null;
                return false;
            }

            long projectedBytes = OpcUaBatchSnapshot.AddSaturating(_queuedBytes, estimatedBytes);
            Exception overflowException = new InvalidOperationException(
                $"구독 로컬 유한 큐 한도를 초과하여 Fail-Closed 종료되었습니다. " +
                $"(QueuedBatches={_queue.Count}/{_options.MaxQueuedBatches}, QueuedBytes={projectedBytes}/{_options.MaxQueuedBytes})");
            TransitionLocked(OpcUaSubscriptionTerminationReason.QueueOverflow, overflowException, clearQueue: true);
            _closeTask ??= CloseServerSubscriptionAsync();
            overflowCloseTask = _closeTask;
            return true;
        }
    }

    private void TransitionLocked(OpcUaSubscriptionTerminationReason reason, Exception? exception, bool clearQueue)
    {
        _terminationReason = reason;
        _terminationException = exception;

        if (clearQueue)
        {
            _queue.Clear();
            _queuedBytes = 0;
        }

        _waiter?.TrySetResult(false);
    }

    private async Task CloseServerSubscriptionAsync()
    {
        try
        {
            await DeleteOnServerSafelyAsync().ConfigureAwait(false);
        }
        finally
        {
            _adapter.Dispose();
        }
    }

    private async Task DeleteOnServerSafelyAsync()
    {
        try
        {
            using CancellationTokenSource timeoutSource = new(_closeTimeout);
            await _adapter.DeleteAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 제한 시간 내 서버 삭제 실패 시 로컬 자원 정리로 진행
        }
        catch (ServiceResultException)
        {
            // 서버 세션이 이미 끊겼을 수 있으므로 로컬 자원 정리로 진행
        }
        catch (ObjectDisposedException)
        {
            // 어댑터가 이미 해제된 경우 무시
        }
    }

    private readonly struct QueuedBatch
    {
        public QueuedBatch(OpcUaDataChangeBatch batch, long estimatedBytes)
        {
            Batch = batch;
            EstimatedBytes = estimatedBytes;
        }

        public OpcUaDataChangeBatch Batch { get; }

        public long EstimatedBytes { get; }
    }
}
