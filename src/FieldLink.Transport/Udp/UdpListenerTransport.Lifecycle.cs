using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Udp;

public sealed partial class UdpListenerTransport
{
    /// <inheritdoc />
    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        long openGeneration = 0;
        CancellationTokenSource? openCancellationSource = null;
        UdpClient? client = null;
        try
        {
            (openGeneration, openCancellationSource) = BeginOpen(cancellationToken);
            openCancellationSource.Token.ThrowIfCancellationRequested();

            var localEndPoint = new IPEndPoint(_options.LocalAddress, _options.LocalPort);
            client = new UdpClient(localEndPoint);
            int actualPort = ((IPEndPoint)client.Client.LocalEndPoint).Port;
            CompleteOpen(client, actualPort, openGeneration);
            client = null;
            return Task.CompletedTask;
        }
        catch (OperationCanceledException exception)
        {
            DisposeClientSafely(client);
            if (openGeneration != 0)
            {
                ResetOpeningToClosed(openGeneration);
            }

            CancellationToken effectiveToken = exception.CancellationToken.IsCancellationRequested
                ? exception.CancellationToken
                : cancellationToken;
            return effectiveToken.IsCancellationRequested
                ? TransportTaskWait.CreateCanceledTask(effectiveToken)
                : Task.FromException(exception);
        }
        catch (ObjectDisposedException exception)
        {
            DisposeClientSafely(client);
            if (openGeneration != 0)
            {
                ResetOpeningToClosed(openGeneration);
            }

            return Task.FromException(exception);
        }
        catch (InvalidOperationException exception) when (openGeneration == 0)
        {
            return Task.FromException(exception);
        }
        catch (Exception exception)
        {
            DisposeClientSafely(client);
            if (openGeneration != 0)
            {
                ResetOpeningToClosed(openGeneration);
            }

            return Task.FromException(new TransportException(
                TransportFailure.ConnectFailed,
                $"로컬 UDP 포트({_options.LocalAddress}:{_options.LocalPort}) 바인딩에 실패했습니다.",
                isRequestSent: false,
                innerException: exception));
        }
        finally
        {
            if (openCancellationSource is not null)
            {
                ReleaseOpenCancellationSource(openCancellationSource);
            }
        }
    }

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        UdpClient? client;
        Task<UdpReceiveResult>? pendingReceive;
        CancellationTokenSource? openToCancel;
        lock (_stateLock)
        {
            if (_state is TransportState.Closed or TransportState.Disposed)
            {
                return Task.CompletedTask;
            }

            _state = TransportState.Closed;
            _openGeneration++;
            _boundPort = 0;
            openToCancel = _openCancellationSource;
            _openCancellationSource = null;
            client = _client;
            pendingReceive = _pendingReceiveTask;
            _client = null;
            _pendingReceiveTask = null;
        }

        CancelSafely(openToCancel);
        TransportTaskWait.ObserveFaultSafely(pendingReceive);
        DisposeClientSafely(client);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        UdpClient? client;
        Task<UdpReceiveResult>? pendingReceive;
        CancellationTokenSource? openToCancel;
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                return;
            }

            _state = TransportState.Disposed;
            _openGeneration++;
            _boundPort = 0;
            openToCancel = _openCancellationSource;
            _openCancellationSource = null;
            client = _client;
            pendingReceive = _pendingReceiveTask;
            _client = null;
            _pendingReceiveTask = null;
        }

        CancelSafely(openToCancel);
        TransportTaskWait.ObserveFaultSafely(pendingReceive);
        DisposeClientSafely(client);
    }

    private (long OpenGeneration, CancellationTokenSource OpenCancellationSource) BeginOpen(CancellationToken callerToken)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(UdpListenerTransport));
            }

            if (_state != TransportState.Closed)
            {
                throw new InvalidOperationException($"현재 상태({_state})에서는 UDP 리스너를 열 수 없습니다.");
            }

            callerToken.ThrowIfCancellationRequested();

            _state = TransportState.Opening;
            _openGeneration++;
            CancellationTokenSource openCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            _openCancellationSource = openCancellationSource;
            return (_openGeneration, openCancellationSource);
        }
    }

    private void CompleteOpen(UdpClient client, int actualPort, long expectedOpenGeneration)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(UdpListenerTransport));
            }

            if (_state != TransportState.Opening || _openGeneration != expectedOpenGeneration)
            {
                throw new OperationCanceledException("UDP 리스너 여는 중 닫혔습니다.");
            }

            _client = client;
            _pendingReceiveTask = null;
            _boundPort = actualPort;
            _state = TransportState.Open;
        }
    }

    private void ResetOpeningToClosed(long expectedOpenGeneration)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Opening && _openGeneration == expectedOpenGeneration)
            {
                _state = TransportState.Closed;
            }
        }
    }

    private void ReleaseOpenCancellationSource(CancellationTokenSource openCancellationSource)
    {
        lock (_stateLock)
        {
            if (ReferenceEquals(_openCancellationSource, openCancellationSource))
            {
                _openCancellationSource = null;
            }
        }

        openCancellationSource.Dispose();
    }

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

    private static void DisposeClientSafely(UdpClient? client)
    {
        try
        {
            client?.Close();
            client?.Dispose();
        }
        catch
        {
            // 소켓 정리 중 예외는 무시
        }
    }
}
