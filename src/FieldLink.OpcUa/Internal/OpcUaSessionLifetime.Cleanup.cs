using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Discovery;
using FieldLink.OpcUa.Security;
using FieldLink.OpcUa.Subscriptions;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa.Internal;

internal sealed partial class OpcUaSessionLifetime
{
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

    private async Task<(IOpcUaSessionAdapter Session, ApplicationConfiguration Configuration)> ConnectAndActivateSessionAsync(
        CancellationToken cancellationToken)
    {
        ApplicationConfiguration configuration = await OpcUaConfigurationFactory
            .CreateClientConfigurationAsync(_options, cancellationToken)
            .ConfigureAwait(false);

        IOpcUaSessionAdapter? session = null;
        try
        {
            IReadOnlyList<EndpointDescription> endpoints = await _discoveryClient
                .GetEndpointsAsync(_options.EndpointUrl, cancellationToken)
                .ConfigureAwait(false);

            ConfiguredEndpoint selectedEndpoint = OpcUaDiscovery.SelectEndpoint(endpoints, _options, configuration);
            session = await _sessionFactory
                .CreateSessionAsync(configuration, selectedEndpoint, _options, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            await session.FetchNamespaceTablesAsync(cancellationToken).ConfigureAwait(false);
            await session.FetchOperationLimitsAsync(cancellationToken).ConfigureAwait(false);
            return (session, configuration);
        }
        catch
        {
            DisposeSessionQuietly(session);
            DisposeConfigurationCertificateQuietly(configuration);
            throw;
        }
    }

    private void CommitOpenedSession(
        IOpcUaSessionAdapter session,
        ApplicationConfiguration configuration,
        long capturedGeneration)
    {
        lock (_stateLock)
        {
            if (_state != OpcUaClientState.Opening || Interlocked.Read(ref _generation) != capturedGeneration)
            {
                throw CreateStateTransitionExceptionLocked(capturedGeneration);
            }

            _sessionLifetimeSource = new CancellationTokenSource();
            _activeSession = session;
            _activeConfiguration = configuration;
            _state = OpcUaClientState.Open;
            session.KeepAlive += (_, args) => OnSessionKeepAlive(capturedGeneration, args);
        }
    }

    private void CleanupFailedOpen(
        IOpcUaSessionAdapter? createdSession,
        ApplicationConfiguration? createdConfiguration,
        long capturedGeneration)
    {
        DisposeSessionQuietly(createdSession);
        DisposeConfigurationCertificateQuietly(createdConfiguration);

        lock (_stateLock)
        {
            if (_state == OpcUaClientState.Opening && Interlocked.Read(ref _generation) == capturedGeneration)
            {
                _state = OpcUaClientState.Closed;
                _activeSession = null;
                _activeConfiguration = null;
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

    private async Task PerformCloseAsync(
        Task? inFlightOpen,
        IOpcUaSessionAdapter? sessionToClose,
        ApplicationConfiguration? configurationToClose,
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
            DisposeConfigurationCertificateQuietly(configurationToClose);
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
                // 이미 폐기된 구독은 추가 정리가 필요하지 않음
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
                int timeoutMilliseconds = OpcUaDeadline.ToPositiveMillisecondsCeiling(
                    remaining,
                    MinimumCloseTimeoutMilliseconds);
                using CancellationTokenSource sessionCloseSource = new(remaining);
                await sessionToClose.CloseAsync(timeoutMilliseconds, sessionCloseSource.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 종료 제한 시간 초과 또는 취소 시 finally에서 세션을 즉시 폐기
        }
        catch (ServiceResultException)
        {
            // 서버 종료 호출 실패 시에도 finally에서 로컬 세션을 폐기
        }
        catch (ObjectDisposedException)
        {
            // 이미 폐기된 세션은 finally에서 안전 정리
        }
        finally
        {
            DisposeSessionQuietly(sessionToClose);
        }
    }

    private Exception SanitizeOrPreserveException(Exception exception)
    {
        string sanitizedMessage = OpcUaDiagnosticRedactor.Redact(exception.Message, _options.UserIdentity);
        if (string.Equals(sanitizedMessage, exception.Message, StringComparison.Ordinal))
        {
            return exception;
        }

        if (exception is ServiceResultException serviceException)
        {
            return new ServiceResultException(serviceException.StatusCode, sanitizedMessage);
        }

        // 원문 exception.Message에 민감 정보가 포함되어 있으므로 InnerException으로 원문 예외를 넘기지 않는다.
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
            // OpenAsync finally와 경합하여 이미 해제된 경우 무시
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
            // 이미 폐기된 세션 무시
        }
    }

    private static void DisposeConfigurationCertificateQuietly(ApplicationConfiguration? configuration)
    {
        try
        {
            configuration?.SecurityConfiguration?.ApplicationCertificate?.DisposeCertificate();
        }
        catch (ObjectDisposedException)
        {
            // 이미 폐기된 인증서 무시
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
            OpcUaDeadline.ObserveOrphanedFault(task);
            return;
        }

        try
        {
            Task delayTask = Task.Delay(timeout);
            Task completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
            if (completed != task)
            {
                OpcUaDeadline.ObserveOrphanedFault(task);
            }
        }
        catch (OperationCanceledException)
        {
            OpcUaDeadline.ObserveOrphanedFault(task);
        }
    }
}
