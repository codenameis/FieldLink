using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Internal;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa.Subscriptions;

/// <summary>SDK 수신 스레드와 단일 소비자(<see cref="ReadNextAsync"/>)를 유한 큐로 분리하는 Fail-Closed OPC UA 구독 핸들입니다.</summary>
public sealed partial class OpcUaSubscription : IDisposable, IAsyncDisposable
{
    private readonly OpcUaSubscriptionOptions _options;
    private readonly IOpcUaSubscriptionAdapter _adapter;
    private readonly OpcUaSessionLifetime _lifetime;
    private readonly long _capturedGeneration;
    private readonly TimeSpan _closeTimeout;
    private readonly Dictionary<uint, (string Key, NodeId NodeId)> _itemsByClientHandle;
    private readonly object _queueLock = new();
    private readonly Queue<QueuedBatch> _queue = new();

    private long _queuedBytes;
    private bool _consumerInFlight;
    private TaskCompletionSource<bool>? _waiter;
    private OpcUaSubscriptionTerminationReason _terminationReason;
    private Exception? _terminationException;
    private Task? _closeTask;

    internal OpcUaSubscription(
        OpcUaSubscriptionOptions options,
        IOpcUaSubscriptionAdapter adapter,
        OpcUaSessionLifetime lifetime,
        long capturedGeneration,
        TimeSpan closeTimeout,
        IReadOnlyList<OpcUaMonitoredItemCreationResult> creationResults)
    {
        _options = options;
        _adapter = adapter;
        _lifetime = lifetime;
        _capturedGeneration = capturedGeneration;
        _closeTimeout = closeTimeout;
        _itemsByClientHandle = new Dictionary<uint, (string Key, NodeId NodeId)>(creationResults.Count);

        OpcUaMonitoredItemResult[] itemResults = new OpcUaMonitoredItemResult[creationResults.Count];
        for (int i = 0; i < creationResults.Count; i++)
        {
            OpcUaMonitoredItemCreationResult item = creationResults[i];
            itemResults[i] = item.Outcome;
            if (item.Outcome.IsSuccess && item.ClientHandle != 0)
            {
                _itemsByClientHandle[item.ClientHandle] = (item.Outcome.ClientItemKey, item.Outcome.ResolvedNodeId);
            }
        }

        ItemResults = new ReadOnlyCollection<OpcUaMonitoredItemResult>(itemResults);
    }

    /// <summary>서버가 할당한 구독 식별자입니다.</summary>
    public uint SubscriptionId => _adapter.SubscriptionId;

    /// <summary>서버가 조정한 최종 발행 주기(ms)입니다.</summary>
    public double RevisedPublishingInterval => _adapter.RevisedPublishingInterval;

    /// <summary>서버가 조정한 KeepAlive 카운트입니다.</summary>
    public uint RevisedKeepAliveCount => _adapter.RevisedKeepAliveCount;

    /// <summary>서버가 조정한 구독 유지 카운트입니다.</summary>
    public uint RevisedLifetimeCount => _adapter.RevisedLifetimeCount;

    /// <summary>요청한 개별 감시 항목의 서버 생성 결과 목록입니다.</summary>
    public IReadOnlyList<OpcUaMonitoredItemResult> ItemResults { get; }

    /// <summary>구독이 과부하·부모 종료·닫기 등으로 종료되었을 때의 종료 원인을 반환합니다.</summary>
    public OpcUaSubscriptionTerminationReason TerminationReason
    {
        get
        {
            lock (_queueLock)
            {
                return _terminationReason;
            }
        }
    }

    /// <summary>과부하(<see cref="OpcUaSubscriptionTerminationReason.QueueOverflow"/>) 또는 부모 세션 손실 등 비정상 종료 시 원인 예외를 반환합니다.</summary>
    public Exception? TerminationException
    {
        get
        {
            lock (_queueLock)
            {
                return _terminationException;
            }
        }
    }

    /// <summary>로컬 유한 큐에서 다음 데이터 변경 배치를 대기하여 읽습니다. 동시에 두 개 이상의 호출은 허용되지 않습니다.</summary>
    /// <param name="cancellationToken">현재 대기 호출만 취소합니다. 취소되어도 구독 자체는 유지됩니다.</param>
    /// <returns>다음 배치, 또는 정상 종료 완료 시 <see langword="null"/>입니다.</returns>
    public async Task<OpcUaDataChangeBatch?> ReadNextAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (TryDequeueOrCreateWaiter(cancellationToken, out OpcUaDataChangeBatch? batch, out Task<bool>? waitTask))
            {
                return batch;
            }

            if (waitTask is null)
            {
                continue;
            }

            try
            {
                await OpcUaDeadline.WaitWithCancellationAsync(waitTask, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_queueLock)
                {
                    _consumerInFlight = false;
                }
            }
        }
    }

    /// <summary>서버 구독 삭제를 시도하고 로컬 큐와 자원을 멱등적으로 종료합니다.</summary>
    /// <param name="cancellationToken">종료 대기 취소 토큰입니다.</param>
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        Task closeTask;
        lock (_queueLock)
        {
            if (_terminationReason == OpcUaSubscriptionTerminationReason.Disposed)
            {
                throw new ObjectDisposedException(nameof(OpcUaSubscription));
            }

            if (_closeTask is null)
            {
                if (_terminationReason == OpcUaSubscriptionTerminationReason.None)
                {
                    TransitionLocked(OpcUaSubscriptionTerminationReason.Closed, null, clearQueue: false);
                }

                _closeTask = CloseServerSubscriptionAsync();
            }

            closeTask = _closeTask;
        }

        _lifetime.UnregisterSubscription(this);
        return OpcUaDeadline.WaitWithCancellationAsync(closeTask, cancellationToken);
    }

    /// <summary>네트워크 응답을 기다리지 않고 구독을 즉시 비블로킹으로 해제합니다.</summary>
    public void Dispose()
    {
        lock (_queueLock)
        {
            if (_terminationReason == OpcUaSubscriptionTerminationReason.Disposed)
            {
                return;
            }

            TransitionLocked(
                OpcUaSubscriptionTerminationReason.Disposed,
                new ObjectDisposedException(nameof(OpcUaSubscription)),
                clearQueue: true);
        }

        _lifetime.UnregisterSubscription(this);
        _adapter.Dispose();
    }

    /// <summary>비동기적으로 구독을 종료하고 로컬 자원을 해제합니다.</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_queueLock)
        {
            if (_terminationReason == OpcUaSubscriptionTerminationReason.Disposed)
            {
                return;
            }
        }

        try
        {
            await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // 동시 Dispose 호출로 이미 해제된 경우 정상 종료로 간주
        }
        finally
        {
            Dispose();
        }
    }
}
