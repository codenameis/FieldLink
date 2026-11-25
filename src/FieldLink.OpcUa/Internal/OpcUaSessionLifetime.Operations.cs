using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Subscriptions;
using FieldLink.OpcUa.Transport;
using Opc.Ua;
using Opc.Ua.Client;

namespace FieldLink.OpcUa.Internal;

internal sealed partial class OpcUaSessionLifetime
{
    public async Task<TResult> ExecuteAsync<TResult>(
        TimeSpan? timeout,
        string operationName,
        Func<IOpcUaSessionAdapter, long, OpcUaDeadline, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        TimeSpan effectiveTimeout = timeout ?? _options.DefaultOperationTimeout;
        OpcUaDeadline deadline = OpcUaDeadline.StartAfter(effectiveTimeout);
        (IOpcUaSessionAdapter session, long capturedGeneration, CancellationToken lifetimeToken, CancellationTokenSource linkedSource) =
            CaptureOpenSessionForOperation(cancellationToken);

        using (linkedSource)
        {
            bool semaphoreAcquired = false;
            try
            {
                await WaitSemaphoreWithDeadlineAsync(deadline, operationName, cancellationToken, linkedSource.Token)
                    .ConfigureAwait(false);
                semaphoreAcquired = true;

                VerifySessionAndGenerationAfterGate(session, capturedGeneration, cancellationToken);

                return await deadline.RunAsync(
                    operationToken => operation(session, capturedGeneration, deadline, operationToken),
                    operationName,
                    linkedSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lifetimeToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw CreateStateTransitionException(capturedGeneration);
            }
            catch (ServiceResultException exception) when (IsFatalSessionStatus(exception.StatusCode))
            {
                TransitionToFaulted(capturedGeneration, SanitizeOrPreserveException(exception));
                throw SanitizeOrPreserveException(exception);
            }
            finally
            {
                if (semaphoreAcquired)
                {
                    _operationSemaphore.Release();
                }
            }
        }
    }

    public bool RegisterSubscription(OpcUaSubscription subscription, long capturedGeneration)
    {
        lock (_stateLock)
        {
            if (_state == OpcUaClientState.Open && Interlocked.Read(ref _generation) == capturedGeneration)
            {
                _activeSubscriptions.Add(subscription);
                return true;
            }

            return false;
        }
    }

    public void UnregisterSubscription(OpcUaSubscription subscription)
    {
        lock (_stateLock)
        {
            _activeSubscriptions.Remove(subscription);
        }
    }

    internal Exception CreateStateTransitionException(long capturedGeneration)
    {
        lock (_stateLock)
        {
            return CreateStateTransitionExceptionLocked(capturedGeneration);
        }
    }

    private (IOpcUaSessionAdapter Session, long Generation, CancellationToken LifetimeToken, CancellationTokenSource LinkedSource) CaptureOpenSessionForOperation(
        CancellationToken callerToken)
    {
        lock (_stateLock)
        {
            if (_state == OpcUaClientState.Disposed)
            {
                throw new ObjectDisposedException(nameof(OpcUaClient));
            }

            if (_state == OpcUaClientState.Faulted)
            {
                throw new InvalidOperationException(
                    "세션이 Faulted 상태입니다. CloseAsync() 호출 후 OpenAsync()로 다시 연결해야 합니다.",
                    _faultException);
            }

            if (_state != OpcUaClientState.Open || _activeSession is null || _sessionLifetimeSource is null)
            {
                throw new InvalidOperationException($"세션이 열려 있지 않습니다. 현재 상태: {_state}.");
            }

            CancellationToken lifetimeToken = _sessionLifetimeSource.Token;
            CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                callerToken,
                lifetimeToken);
            return (_activeSession, Interlocked.Read(ref _generation), lifetimeToken, linkedSource);
        }
    }

    private async Task WaitSemaphoreWithDeadlineAsync(
        OpcUaDeadline deadline,
        string operationName,
        CancellationToken callerToken,
        CancellationToken linkedToken)
    {
        TimeSpan remaining = deadline.ThrowIfExpiredOrCanceled(callerToken, operationName);
        bool entered = await _operationSemaphore.WaitAsync(remaining, linkedToken).ConfigureAwait(false);
        if (!entered)
        {
            throw new TimeoutException($"{operationName} 동시성 슬롯 대기 중 전체 제한 시간이 초과되었습니다.");
        }
    }

    private void VerifySessionAndGenerationAfterGate(
        IOpcUaSessionAdapter expectedSession,
        long capturedGeneration,
        CancellationToken callerToken)
    {
        callerToken.ThrowIfCancellationRequested();

        lock (_stateLock)
        {
            if (_state == OpcUaClientState.Disposed)
            {
                throw new ObjectDisposedException(nameof(OpcUaClient));
            }

            if (_state != OpcUaClientState.Open ||
                Interlocked.Read(ref _generation) != capturedGeneration ||
                !ReferenceEquals(_activeSession, expectedSession))
            {
                throw CreateStateTransitionExceptionLocked(capturedGeneration);
            }
        }
    }

    private Exception CreateStateTransitionExceptionLocked(long capturedGeneration)
    {
        if (_state == OpcUaClientState.Disposed)
        {
            return new ObjectDisposedException(nameof(OpcUaClient));
        }

        if (_state == OpcUaClientState.Faulted)
        {
            return new InvalidOperationException("요청 대기 또는 실행 중 세션이 Faulted 상태가 되었습니다.", _faultException);
        }

        if (Interlocked.Read(ref _generation) != capturedGeneration)
        {
            return new InvalidOperationException("세션 세대가 변경되거나 종료되어 대기 중이던 요청을 중단했습니다.");
        }

        return new InvalidOperationException($"세션 상태 변경({_state})으로 인해 요청을 진행할 수 없습니다.");
    }

    private void OnSessionKeepAlive(long capturedGeneration, KeepAliveEventArgs args)
    {
        if (CurrentGeneration != capturedGeneration)
        {
            return;
        }

        if (args.Status is null || ServiceResult.IsGood(args.Status))
        {
            Interlocked.Exchange(ref _consecutiveKeepAliveFailures, 0);
            return;
        }

        int failures = Interlocked.Increment(ref _consecutiveKeepAliveFailures);
        if (failures >= _options.MaxKeepAliveFailuresBeforeFault || IsFatalSessionStatus(args.Status.StatusCode))
        {
            TransitionToFaulted(capturedGeneration, new ServiceResultException(args.Status));
        }
    }

    private void TransitionToFaulted(long capturedGeneration, Exception cause)
    {
        CancellationTokenSource? lifetimeToCancel;
        List<OpcUaSubscription> subscriptions = new();

        lock (_stateLock)
        {
            if (_state != OpcUaClientState.Open || Interlocked.Read(ref _generation) != capturedGeneration)
            {
                return;
            }

            _state = OpcUaClientState.Faulted;
            _faultException = cause;
            lifetimeToCancel = _sessionLifetimeSource;
            _sessionLifetimeSource = null;
            subscriptions.AddRange(_activeSubscriptions);
            _activeSubscriptions.Clear();
        }

        CancelAndDisposeSafely(lifetimeToCancel);
        for (int i = 0; i < subscriptions.Count; i++)
        {
            subscriptions[i].TerminateFromParent(OpcUaSubscriptionTerminationReason.ParentFaulted, cause);
        }
    }
}
