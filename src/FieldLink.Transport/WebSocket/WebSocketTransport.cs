using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.WebSocket;

/// <summary><see cref="ClientWebSocket"/>을 기반으로 전이중 텍스트·이진 메시지를 송수신하는 전송 구현체입니다.</summary>
public sealed class WebSocketTransport : IWebSocketTransport
{
    private const int ReceiveChunkSize = 4096;
    private const string NormalClosureDescription = "Closed by client";

    private readonly WebSocketTransportOptions _options;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _receiveLock = new(1, 1);
    private readonly object _stateLock = new();

    private ClientWebSocket? _socket;
    private TransportState _state = TransportState.Closed;

    /// <summary>WebSocket 설정으로 전송 인스턴스를 초기화합니다.</summary>
    /// <param name="options">서버 URI, 제한 시간, 하위 프로토콜, 최대 메시지 크기 설정입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>가 null일 때.</exception>
    public WebSocketTransport(WebSocketTransportOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public TransportState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc />
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        BeginOpen();

        ClientWebSocket socket = CreateConfiguredSocket();
        using var timeoutCts = new CancellationTokenSource(_options.ConnectTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await socket.ConnectAsync(_options.ServerUri, linkedCts.Token).ConfigureAwait(false);
            CompleteOpen(socket);
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            ResetOpeningToClosed();
            throw new TransportException(
                TransportFailure.ConnectFailed,
                $"WebSocket 연결 제한 시간({_options.ConnectTimeout.TotalMilliseconds}ms)이 초과되었습니다.",
                isRequestSent: false,
                innerException: new TimeoutException("WebSocket 연결 시간 초과", exception));
        }
        catch (OperationCanceledException)
        {
            socket.Dispose();
            ResetOpeningToClosed();
            throw;
        }
        catch (ObjectDisposedException)
        {
            socket.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            socket.Dispose();
            ResetOpeningToClosed();
            throw new TransportException(
                TransportFailure.ConnectFailed,
                $"WebSocket 서버({_options.ServerUri}) 연결에 실패했습니다.",
                isRequestSent: false,
                innerException: exception);
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        ClientWebSocket? socket = DetachSocketIfNotClosed();
        if (socket is not null)
        {
            await CloseSocketSafelyAsync(socket, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(byte[] payload, WebSocketMessageKind kind = WebSocketMessageKind.Text, CancellationToken cancellationToken = default)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        WebSocketMessageType messageType = MapMessageType(kind);
        _ = EnsureOpenAndGetSocket();

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ClientWebSocket socket = EnsureOpenAndGetSocket();
            await socket.SendAsync(new ArraySegment<byte>(payload), messageType, endOfMessage: true, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            AbortSocketOnFailure();
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            throw new TransportException(TransportFailure.ConnectionClosed, "WebSocket 연결이 닫혀 전송할 수 없습니다.", isRequestSent: false, innerException: exception);
        }
        catch (Exception exception)
        {
            throw new TransportException(TransportFailure.SendFailed, "WebSocket 메시지 전송 중 오류가 발생했습니다.", isRequestSent: true, innerException: exception);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<WebSocketFrameMessage> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        _ = EnsureOpenAndGetSocket();

        await _receiveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ClientWebSocket socket = EnsureOpenAndGetSocket();
            return await ReceiveFullMessageAsync(socket, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            AbortSocketOnFailure();
            throw;
        }
        catch (TransportException)
        {
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            throw new TransportException(TransportFailure.ConnectionClosed, "WebSocket 연결이 닫혀 수신할 수 없습니다.", isRequestSent: false, innerException: exception);
        }
        catch (Exception exception)
        {
            throw new TransportException(TransportFailure.ReceiveFailed, "WebSocket 메시지 수신 중 오류가 발생했습니다.", isRequestSent: false, innerException: exception);
        }
        finally
        {
            _receiveLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ClientWebSocket? socket;
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                return;
            }

            _state = TransportState.Disposed;
            socket = _socket;
            _socket = null;
        }

        AbortAndDisposeSocket(socket);
    }

    private void BeginOpen()
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

            _state = TransportState.Opening;
        }
    }

    private void CompleteOpen(ClientWebSocket socket)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(WebSocketTransport));
            }

            if (_state != TransportState.Opening)
            {
                throw new OperationCanceledException("WebSocket 연결 중 전송이 닫혔습니다.");
            }

            _socket = socket;
            _state = TransportState.Open;
        }
    }

    private void ResetOpeningToClosed()
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Opening)
            {
                _state = TransportState.Closed;
            }
        }
    }

    private ClientWebSocket? DetachSocketIfNotClosed()
    {
        lock (_stateLock)
        {
            if (_state is TransportState.Closed or TransportState.Disposed)
            {
                return null;
            }

            ClientWebSocket? socket = _socket;
            _socket = null;
            _state = TransportState.Closed;
            return socket;
        }
    }

    private void AbortSocketOnFailure()
    {
        ClientWebSocket? socket = DetachSocketIfNotClosed();
        AbortAndDisposeSocket(socket);
    }

    private static WebSocketMessageType MapMessageType(WebSocketMessageKind kind) =>
        kind switch
        {
            WebSocketMessageKind.Text => WebSocketMessageType.Text,
            WebSocketMessageKind.Binary => WebSocketMessageType.Binary,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "지원하지 않는 WebSocket 메시지 형식입니다.")
        };

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

    private async Task<WebSocketFrameMessage> ReceiveFullMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        byte[] chunk = new byte[ReceiveChunkSize];
        using var accumulator = new MemoryStream();
        WebSocketMessageKind? messageKind = null;

        while (true)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(chunk), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                AbortSocketOnFailure();
                throw new TransportException(TransportFailure.ConnectionClosed, "원격 호스트에서 WebSocket 연결을 닫았습니다.", isRequestSent: false);
            }

            messageKind ??= result.MessageType == WebSocketMessageType.Text ? WebSocketMessageKind.Text : WebSocketMessageKind.Binary;
            AppendFragmentOrThrow(accumulator, chunk, result.Count);

            if (result.EndOfMessage)
            {
                return new WebSocketFrameMessage(messageKind.Value, accumulator.ToArray());
            }
        }
    }

    private void AppendFragmentOrThrow(MemoryStream accumulator, byte[] chunk, int count)
    {
        if (accumulator.Length + count > _options.MaximumMessageSize)
        {
            AbortSocketOnFailure();
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"수신된 WebSocket 메시지가 최대 허용 크기({_options.MaximumMessageSize} 바이트)를 초과했습니다.",
                isRequestSent: false);
        }

        if (count > 0)
        {
            accumulator.Write(chunk, 0, count);
        }
    }

    private static async Task CloseSocketSafelyAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, NormalClosureDescription, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // 종료 핸드셰이크 실패 시 강제 정리를 계속 진행
        }
        finally
        {
            socket.Dispose();
        }
    }

    private static void AbortAndDisposeSocket(ClientWebSocket? socket)
    {
        try
        {
            socket?.Abort();
            socket?.Dispose();
        }
        catch
        {
            // 소켓 정리 중 예외는 무시
        }
    }

    private ClientWebSocket EnsureOpenAndGetSocket()
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(WebSocketTransport));
            }

            if (_state != TransportState.Open || _socket is null)
            {
                throw new InvalidOperationException("WebSocket이 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
            }

            return _socket;
        }
    }

    private sealed class BypassWebProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public Uri GetProxy(Uri destination) => destination;

        public bool IsBypassed(Uri host) => true;
    }
}
