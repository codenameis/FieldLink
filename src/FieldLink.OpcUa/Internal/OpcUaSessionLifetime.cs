using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Discovery;
using FieldLink.OpcUa.Security;
using FieldLink.OpcUa.Subscriptions;
using FieldLink.OpcUa.Transport;
using Opc.Ua;
using Opc.Ua.Client;

namespace FieldLink.OpcUa.Internal;

internal sealed class OpcUaSessionLifetime : IDisposable
{
    private const int MinimumCloseTimeoutMilliseconds = 100;

    private static readonly HashSet<uint> FatalSessionStatusCodes = new()
    {
        StatusCodes.BadSessionIdInvalid,
        StatusCodes.BadSessionClosed,
        StatusCodes.BadSessionNotActivated,
        StatusCodes.BadSecureChannelClosed,
        StatusCodes.BadSecureChannelIdInvalid,
        StatusCodes.BadConnectionClosed,
        StatusCodes.BadNotConnected,
        StatusCodes.BadCommunicationError
    };

    private readonly OpcUaClientOptions _options;
    private readonly IOpcUaDiscoveryClient _discoveryClient;
    private readonly IOpcUaSessionFactory _sessionFactory;
    private readonly SemaphoreSlim _operationSemaphore;
    private readonly object _stateLock = new();
    private readonly List<OpcUaSubscription> _activeSubscriptions = new();

    private OpcUaClientState _state = OpcUaClientState.Closed;
    private long _generation;
    private int _consecutiveKeepAliveFailures;
    private IOpcUaSessionAdapter? _activeSession;
    private Exception? _faultException;
    private CancellationTokenSource? _openCancellationSource;
    private CancellationTokenSource? _sessionLifetimeSource;
    private Task? _openTask;
    private Task? _closeTask;

    public OpcUaSessionLifetime(
        OpcUaClientOptions options,
        IOpcUaDiscoveryClient discoveryClient,
        IOpcUaSessionFactory sessionFactory)
    {
        _options = options;
        _discoveryClient = discoveryClient;
        _sessionFactory = sessionFactory;
        _operationSemaphore = new SemaphoreSlim(options.MaxConcurrentOperations, options.MaxConcurrentOperations);
    }

    public OpcUaClientState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    public long CurrentGeneration => Volatile.Read(ref _generation);

    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> openCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        long capturedGeneration;
        CancellationTokenSource localOpenSource;
        CancellationToken openToken;

        lock (_stateLock)
        {
            ThrowIfNotClosedForOpenLocked();
            cancellationToken.ThrowIfCancellationRequested();

            _state = OpcUaClientState.Opening;
            _generation++;
            capturedGeneration = _generation;
            _consecutiveKeepAliveFailures = 0;
            _faultException = null;
            localOpenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _openCancellationSource = localOpenSource;
            openToken = localOpenSource.Token;
            _openTask = openCompletion.Task;
        }

        IOpcUaSessionAdapter? createdSession = null;
        try
        {
            OpcUaDeadline deadline = OpcUaDeadline.StartAfter(_options.ConnectTimeout);
            createdSession = await deadline.RunAsync(
                connectToken => ConnectAndActivateSessionAsync(connectToken),
                nameof(OpenAsync),
                openToken).ConfigureAwait(false);

            CommitOpenedSession(createdSession, capturedGeneration);
            createdSession = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ObjectDisposedException)
        {
            CleanupFailedOpen(createdSession, capturedGeneration);
            throw SanitizeOrPreserveException(ex);
        }
        catch
        {
            CleanupFailedOpen(createdSession, capturedGeneration);
            throw;
        }
        finally
        {
            ReleaseOpenCancellationSource(localOpenSource);
            openCompletion.TrySetResult(true);
        }
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        TimeSpan? timeout,
        string operationName,
        Func<IOpcUaSessionAdapter, OpcUaDeadline, CancellationToken, Task<TResult>> operation,
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
                    operationToken => operation(session, deadline, operationToken),
                    operationName,
                    linkedSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lifetimeToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw CreateStateTransitionException(capturedGeneration);
            }
            catch (ServiceResultException ex) when (IsFatalSessionStatus(ex.StatusCode))
            {
                TransitionToFaulted(capturedGeneration, SanitizeOrPreserveException(ex));
                throw SanitizeOrPreserveException(ex);
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

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        Task closeTask;
        CancellationTokenSource? openToCancel = null;
        CancellationTokenSource? lifetimeToCancel = null;
        List<OpcUaSubscription> subscriptionsToClose = new();

        lock (_stateLock)
        {
            if (_state == OpcUaClientState.Disposed)
            {
                throw new ObjectDisposedException(nameof(OpcUaClient));
            }

            if (_state == OpcUaClientState.Closed)
            {
                return Task.CompletedTask;
            }

            if (_state == OpcUaClientState.Closing && _closeTask is not null)
            {
                return WaitWithCancellationAsync(_closeTask, cancellationToken);
            }

            _state = OpcUaClientState.Closing;
            _generation++;
            openToCancel = _openCancellationSource;
            _openCancellationSource = null;
            lifetimeToCancel = _sessionLifetimeSource;
            _sessionLifetimeSource = null;
            subscriptionsToClose.AddRange(_activeSubscriptions);
            _activeSubscriptions.Clear();

            IOpcUaSessionAdapter? sessionToClose = _activeSession;
            _activeSession = null;
            Task? inFlightOpen = _openTask;

            _closeTask = PerformCloseAsync(inFlightOpen, sessionToClose, subscriptionsToClose);
            closeTask = _closeTask;
        }

        CancelSafely(openToCancel);
        CancelAndDisposeSafely(lifetimeToCancel);
        return WaitWithCancellationAsync(closeTask, cancellationToken);
    }

    public bool RegisterSubscription(OpcUaSubscription subscription, long capturedGeneration)
    {
        lock (_stateLock)
        {
            if (_state == OpcUaClientState.Open && _generation == capturedGeneration)
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

    public void Dispose()
    {
        CancellationTokenSource? openToCancel;
        CancellationTokenSource? lifetimeToCancel;
        IOpcUaSessionAdapter? sessionToDispose;
        List<OpcUaSubscription> subscriptionsToDispose = new();

        lock (_stateLock)
        {
            if (_state == OpcUaClientState.Disposed)
            {
                return;
            }

            _state = OpcUaClientState.Disposed;
            _generation++;
            openToCancel = _openCancellationSource;
            lifetimeToCancel = _sessionLifetimeSource;
            _openCancellationSource = null;
            _sessionLifetimeSource = null;
            sessionToDispose = _activeSession;
            _activeSession = null;
            subscriptionsToDispose.AddRange(_activeSubscriptions);
            _activeSubscriptions.Clear();
        }

        CancelSafely(openToCancel);
        CancelAndDisposeSafely(lifetimeToCancel);
        DisposeSubscriptionsOnDispose(subscriptionsToDispose);
        DisposeSessionQuietly(sessionToDispose);
        openToCancel?.Dispose();
    }

    private void ThrowIfNotClosedForOpenLocked()
    {
        if (_state == OpcUaClientState.Disposed)
        {
            throw new ObjectDisposedException(nameof(OpcUaClient));
        }

        if (_state != OpcUaClientState.Closed)
        {
            throw new InvalidOperationException(
                $"현재 상태({_state})에서는 OpenAsync를 호출할 수 없습니다. Closed 상태여야 합니다.");
        }
    }

    private async Task<IOpcUaSessionAdapter> ConnectAndActivateSessionAsync(CancellationToken cancellationToken)
    {
        ApplicationConfiguration configuration = await OpcUaConfigurationFactory
            .CreateClientConfigurationAsync(_options, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<EndpointDescription> endpoints = await _discoveryClient
            .GetEndpointsAsync(_options.EndpointUrl, cancellationToken)
            .ConfigureAwait(false);

        ConfiguredEndpoint selectedEndpoint = OpcUaDiscovery.SelectEndpoint(endpoints, _options);
        IOpcUaSessionAdapter session = await _sessionFactory
            .CreateSessionAsync(configuration, selectedEndpoint, _options, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await session.FetchNamespaceTablesAsync(cancellationToken).ConfigureAwait(false);
            await session.FetchOperationLimitsAsync(cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch
        {
            DisposeSessionQuietly(session);
            throw;
        }
    }

    private void CommitOpenedSession(IOpcUaSessionAdapter session, long capturedGeneration)
    {
        lock (_stateLock)
        {
            if (_state != OpcUaClientState.Opening || _generation != capturedGeneration)
            {
                throw CreateStateTransitionExceptionLocked(capturedGeneration);
            }

            _sessionLifetimeSource = new CancellationTokenSource();
            _activeSession = session;
            _state = OpcUaClientState.Open;
            session.KeepAlive += (_, args) => OnSessionKeepAlive(capturedGeneration, args);
        }
    }

    private void CleanupFailedOpen(IOpcUaSessionAdapter? createdSession, long capturedGeneration)
    {
        DisposeSessionQuietly(createdSession);

        lock (_stateLock)
        {
            if (_state == OpcUaClientState.Opening && _generation == capturedGeneration)
            {
                _state = OpcUaClientState.Closed;
                _activeSession = null;
            }
        }
    }

    private void ReleaseOpenCancellationSource(CancellationTokenSource localOpenSource)
    {
        lock (_stateLock)
        {
            if (ReferenceEquals(_openCancellationSource, localOpenSource))
            {
                _openCancellationSource = null;
            }
        }

        localOpenSource.Dispose();
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
            return (_activeSession, _generation, lifetimeToken, linkedSource);
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
                _generation != capturedGeneration ||
                !ReferenceEquals(_activeSession, expectedSession))
            {
                throw CreateStateTransitionExceptionLocked(capturedGeneration);
            }
        }
    }

    internal Exception CreateStateTransitionException(long capturedGeneration)
    {
        lock (_stateLock)
        {
            return CreateStateTransitionExceptionLocked(capturedGeneration);
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

        if (_generation != capturedGeneration)
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
            if (_state != OpcUaClientState.Open || _generation != capturedGeneration)
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

    private async Task PerformCloseAsync(
        Task? inFlightOpen,
        IOpcUaSessionAdapter? sessionToClose,
        List<OpcUaSubscription> subscriptionsToClose)
    {
        OpcUaDeadline closeDeadline = OpcUaDeadline.StartAfter(_options.CloseTimeout);
        try
        {
            if (inFlightOpen is not null && !inFlightOpen.IsCompleted)
            {
                await WaitBoundedQuietlyAsync(inFlightOpen, closeDeadline.Remaining).ConfigureAwait(false);
            }

            await CloseSubscriptionsWithinBudgetAsync(subscriptionsToClose, closeDeadline).ConfigureAwait(false);
            await CloseSessionWithinBudgetAsync(sessionToClose, closeDeadline).ConfigureAwait(false);
        }
        finally
        {
            lock (_stateLock)
            {
                if (_state == OpcUaClientState.Closing)
                {
                    _state = OpcUaClientState.Closed;
                    _faultException = null;
                    _closeTask = null;
                    _openTask = null;
                }
            }
        }
    }

    private static async Task CloseSubscriptionsWithinBudgetAsync(
        List<OpcUaSubscription> subscriptionsToClose,
        OpcUaDeadline closeDeadline)
    {
        for (int i = 0; i < subscriptionsToClose.Count; i++)
        {
            subscriptionsToClose[i].TerminateFromParent(OpcUaSubscriptionTerminationReason.ParentClosed, null);
            TimeSpan remaining = closeDeadline.Remaining;
            if (remaining <= TimeSpan.Zero)
            {
                subscriptionsToClose[i].Dispose();
                continue;
            }

            try
            {
                using CancellationTokenSource subscriptionCloseSource = new(remaining);
                await subscriptionsToClose[i].CloseAsync(subscriptionCloseSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                subscriptionsToClose[i].Dispose();
            }
            catch (ServiceResultException)
            {
                subscriptionsToClose[i].Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private static async Task CloseSessionWithinBudgetAsync(
        IOpcUaSessionAdapter? sessionToClose,
        OpcUaDeadline closeDeadline)
    {
        if (sessionToClose is null)
        {
            return;
        }

        try
        {
            TimeSpan remaining = closeDeadline.Remaining;
            if (remaining > TimeSpan.Zero)
            {
                int timeoutMilliseconds = Math.Max(
                    MinimumCloseTimeoutMilliseconds,
                    (int)Math.Min(int.MaxValue, remaining.TotalMilliseconds));
                using CancellationTokenSource sessionCloseSource = new(remaining);
                await sessionToClose.CloseAsync(timeoutMilliseconds, sessionCloseSource.Token).ConfigureAwait(false);
            }
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
            DisposeSessionQuietly(sessionToClose);
        }
    }

    private Exception SanitizeOrPreserveException(Exception ex)
    {
        string sanitizedMessage = OpcUaDiagnosticRedactor.Redact(ex.Message, _options.UserIdentity);
        if (string.Equals(sanitizedMessage, ex.Message, StringComparison.Ordinal))
        {
            return ex;
        }

        if (ex is ServiceResultException serviceException)
        {
            return new ServiceResultException(serviceException.StatusCode, sanitizedMessage);
        }

        // 원문 ex.Message에 민감 정보가 포함되어 있으므로 InnerException으로 원문 예외를 넘기지 않는다.
        return new InvalidOperationException(sanitizedMessage);
    }

    private static bool IsFatalSessionStatus(StatusCode statusCode) =>
        FatalSessionStatusCodes.Contains(statusCode.Code);

    private static void CancelSafely(CancellationTokenSource? cancellationSource)
    {
        if (cancellationSource is null)
        {
            return;
        }

        try
        {
            cancellationSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void CancelAndDisposeSafely(CancellationTokenSource? cancellationSource)
    {
        if (cancellationSource is null)
        {
            return;
        }

        CancelSafely(cancellationSource);
        cancellationSource.Dispose();
    }

    private static void DisposeSessionQuietly(IOpcUaSessionAdapter? session)
    {
        if (session is null)
        {
            return;
        }

        try
        {
            session.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void DisposeSubscriptionsOnDispose(List<OpcUaSubscription> subscriptions)
    {
        for (int i = 0; i < subscriptions.Count; i++)
        {
            subscriptions[i].Dispose();
        }
    }

    private static async Task WaitBoundedQuietlyAsync(Task task, TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            Task delayTask = Task.Delay(timeout);
            await Task.WhenAny(task, delayTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task WaitWithCancellationAsync(Task targetTask, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            await targetTask.ConfigureAwait(false);
            return;
        }

        TaskCompletionSource<bool> cancelSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(
            // Register에 전달한 state는 직전 줄의 cancelSource 인스턴스다.
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(false),
            cancelSource);

        Task completed = await Task.WhenAny(targetTask, cancelSource.Task).ConfigureAwait(false);
        if (completed == cancelSource.Task)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        await targetTask.ConfigureAwait(false);
    }
}
