using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.WebSocket;

public sealed partial class WebSocketTransport
{
    /// <inheritdoc />
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        (long openGeneration, CancellationTokenSource openCancellationSource) = BeginOpen(cancellationToken);

        ClientWebSocket? socket = null;
        try
        {
            socket = CreateConfiguredSocket();
            using CancellationTokenSource timeoutSource = new(_options.ConnectTimeout);
            using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                openCancellationSource.Token,
                timeoutSource.Token);

            try
            {
                await socket.ConnectAsync(_options.ServerUri, linkedSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (
                timeoutSource.IsCancellationRequested &&
                !openCancellationSource.IsCancellationRequested)
            {
                throw new TransportException(
                    TransportFailure.ConnectFailed,
                    $"WebSocket 연결 제한 시간({_options.ConnectTimeout.TotalMilliseconds}ms)이 초과되었습니다.",
                    isRequestSent: false,
                    innerException: new TimeoutException("WebSocket 연결 시간 초과", exception));
            }

            CompleteOpen(socket, openGeneration);
            socket = null;
        }
        catch (TransportException)
        {
            AbortAndDisposeSocket(socket);
            ResetOpeningToClosed(openGeneration);
            throw;
        }
        catch (OperationCanceledException)
        {
            AbortAndDisposeSocket(socket);
            ResetOpeningToClosed(openGeneration);
            throw;
        }
        catch (ObjectDisposedException)
        {
            AbortAndDisposeSocket(socket);
            ResetOpeningToClosed(openGeneration);
            throw;
        }
        catch (Exception exception)
        {
            AbortAndDisposeSocket(socket);
            ResetOpeningToClosed(openGeneration);
            throw new TransportException(
                TransportFailure.ConnectFailed,
                $"WebSocket 서버({_options.ServerUri}) 연결에 실패했습니다.",
                isRequestSent: false,
                innerException: exception);
        }
        finally
        {
            ReleaseOpenCancellationSource(openCancellationSource);
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        ClientWebSocket? socket = DetachSocketAndCancelOpen();
        if (socket is not null)
        {
            await CloseSocketSafelyAsync(socket, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ClientWebSocket? socket;
        CancellationTokenSource? openToCancel;
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                return;
            }

            _state = TransportState.Disposed;
            _openGeneration++;
            openToCancel = _openCancellationSource;
            _openCancellationSource = null;
            socket = _socket;
            _socket = null;
        }

        CancelSafely(openToCancel);
        AbortAndDisposeSocket(socket);
    }

    private (long OpenGeneration, CancellationTokenSource OpenCancellationSource) BeginOpen(CancellationToken callerToken)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(WebSocketTransport));
            }

            if (_state != TransportState.Closed)
            {
                throw new InvalidOperationException($"현재 상태({_state})에서는 WebSocket을 열 수 없습니다.");
            }

            callerToken.ThrowIfCancellationRequested();

            _state = TransportState.Opening;
            _openGeneration++;
            CancellationTokenSource openCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            _openCancellationSource = openCancellationSource;
            return (_openGeneration, openCancellationSource);
        }
    }

    private ClientWebSocket CreateConfiguredSocket()
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = _options.KeepAliveInterval;

        if (!_options.UseProxy)
        {
            socket.Options.Proxy = new BypassWebProxy();
        }

        foreach (string subProtocol in _options.SubProtocols)
        {
            socket.Options.AddSubProtocol(subProtocol);
        }

        foreach (KeyValuePair<string, string> header in _options.Headers)
        {
            socket.Options.SetRequestHeader(header.Key, header.Value);
        }

        return socket;
    }

    private void CompleteOpen(ClientWebSocket socket, long expectedOpenGeneration)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(WebSocketTransport));
            }

            if (_state != TransportState.Opening || _openGeneration != expectedOpenGeneration)
            {
                throw new OperationCanceledException("WebSocket 연결 중 전송이 닫혔습니다.");
            }

            _socket = socket;
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

    private ClientWebSocket? DetachSocketAndCancelOpen()
    {
        ClientWebSocket? socket;
        CancellationTokenSource? openToCancel;
        lock (_stateLock)
        {
            if (_state is TransportState.Closed or TransportState.Disposed)
            {
                return null;
            }

            _state = TransportState.Closed;
            _openGeneration++;
            openToCancel = _openCancellationSource;
            _openCancellationSource = null;
            socket = _socket;
            _socket = null;
        }

        CancelSafely(openToCancel);
        return socket;
    }

    private bool AbortSocketIfCurrent(ClientWebSocket? failedSocket)
    {
        if (failedSocket is null)
        {
            return false;
        }

        ClientWebSocket? detached = null;
        lock (_stateLock)
        {
            if (_state != TransportState.Disposed && ReferenceEquals(_socket, failedSocket))
            {
                detached = _socket;
                _socket = null;
                _state = TransportState.Closed;
            }
        }

        if (detached is null)
        {
            return false;
        }

        AbortAndDisposeSocket(detached);
        return true;
    }
}
