using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Subscriptions;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa.Internal;

internal sealed partial class OpcUaSessionLifetime : IDisposable
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
    private ApplicationConfiguration? _activeConfiguration;
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

    public long CurrentGeneration => Interlocked.Read(ref _generation);

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
            capturedGeneration = Interlocked.Increment(ref _generation);
            _consecutiveKeepAliveFailures = 0;
            _faultException = null;
            localOpenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _openCancellationSource = localOpenSource;
            openToken = localOpenSource.Token;
            _openTask = openCompletion.Task;
        }

        IOpcUaSessionAdapter? createdSession = null;
        ApplicationConfiguration? createdConfiguration = null;
        try
        {
            OpcUaDeadline deadline = OpcUaDeadline.StartAfter(_options.ConnectTimeout);
            (createdSession, createdConfiguration) = await deadline.RunAsync(
                connectToken => ConnectAndActivateSessionAsync(connectToken),
                nameof(OpenAsync),
                openToken).ConfigureAwait(false);

            CommitOpenedSession(createdSession, createdConfiguration, capturedGeneration);
            createdSession = null;
            createdConfiguration = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not ObjectDisposedException)
        {
            CleanupFailedOpen(createdSession, createdConfiguration, capturedGeneration);
            throw SanitizeOrPreserveException(exception);
        }
        catch
        {
            CleanupFailedOpen(createdSession, createdConfiguration, capturedGeneration);
            throw;
        }
        finally
        {
            ReleaseOpenCancellationSource(localOpenSource);
            openCompletion.TrySetResult(true);
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
                return OpcUaDeadline.WaitWithCancellationAsync(_closeTask, cancellationToken);
            }

            _state = OpcUaClientState.Closing;
            Interlocked.Increment(ref _generation);
            openToCancel = _openCancellationSource;
            _openCancellationSource = null;
            lifetimeToCancel = _sessionLifetimeSource;
            _sessionLifetimeSource = null;
            subscriptionsToClose.AddRange(_activeSubscriptions);
            _activeSubscriptions.Clear();

            IOpcUaSessionAdapter? sessionToClose = _activeSession;
            ApplicationConfiguration? configurationToClose = _activeConfiguration;
            _activeSession = null;
            _activeConfiguration = null;
            Task? inFlightOpen = _openTask;

            _closeTask = PerformCloseAsync(inFlightOpen, sessionToClose, configurationToClose, subscriptionsToClose);
            closeTask = _closeTask;
        }

        CancelSafely(openToCancel);
        CancelAndDisposeSafely(lifetimeToCancel);
        return OpcUaDeadline.WaitWithCancellationAsync(closeTask, cancellationToken);
    }

    public void Dispose()
    {
        CancellationTokenSource? openToCancel;
        CancellationTokenSource? lifetimeToCancel;
        IOpcUaSessionAdapter? sessionToDispose;
        ApplicationConfiguration? configurationToDispose;
        List<OpcUaSubscription> subscriptionsToDispose = new();

        lock (_stateLock)
        {
            if (_state == OpcUaClientState.Disposed)
            {
                return;
            }

            _state = OpcUaClientState.Disposed;
            Interlocked.Increment(ref _generation);
            openToCancel = _openCancellationSource;
            lifetimeToCancel = _sessionLifetimeSource;
            _openCancellationSource = null;
            _sessionLifetimeSource = null;
            sessionToDispose = _activeSession;
            configurationToDispose = _activeConfiguration;
            _activeSession = null;
            _activeConfiguration = null;
            subscriptionsToDispose.AddRange(_activeSubscriptions);
            _activeSubscriptions.Clear();
        }

        // _openCancellationSource의 Dispose는 OpenAsync finally(ReleaseOpenCancellationSource)가 단독으로 소유한다.
        CancelSafely(openToCancel);
        CancelAndDisposeSafely(lifetimeToCancel);
        DisposeSubscriptionsOnDispose(subscriptionsToDispose);
        DisposeSessionQuietly(sessionToDispose);
        DisposeConfigurationCertificateQuietly(configurationToDispose);
    }
}
