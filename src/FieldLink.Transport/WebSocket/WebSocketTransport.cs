using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.WebSocket;

/// <summary><see cref="ClientWebSocket"/>을 기반으로 전이중 텍스트·이진 메시지를 송수신하는 전송 구현체입니다.</summary>
public sealed partial class WebSocketTransport : IWebSocketTransport
{
    private const int ReceiveChunkSize = 4096;
    private const int CloseSendLockWaitMilliseconds = 2000;
    private const string NormalClosureDescription = "Closed by client";

    private readonly WebSocketTransportOptions _options;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _receiveLock = new(1, 1);
    private readonly object _stateLock = new();

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _openCancellationSource;
    private long _openGeneration;
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
    public async Task SendAsync(byte[] payload, WebSocketMessageKind kind = WebSocketMessageKind.Text, CancellationToken cancellationToken = default)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        WebSocketMessageType messageType = MapMessageType(kind);
        _ = EnsureOpenAndGetSocket();

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        ClientWebSocket? activeSocket = null;
        try
        {
            activeSocket = EnsureOpenAndGetSocket();
            await activeSocket.SendAsync(new ArraySegment<byte>(payload), messageType, endOfMessage: true, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AbortSocketIfCurrent(activeSocket);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            AbortSocketIfCurrent(activeSocket);
            throw new TransportException(TransportFailure.ConnectionClosed, "WebSocket 연결이 닫혀 전송할 수 없습니다.", isRequestSent: false, innerException: exception);
        }
        catch (ObjectDisposedException exception)
        {
            AbortSocketIfCurrent(activeSocket);
            throw new TransportException(TransportFailure.ConnectionClosed, "WebSocket 연결이 닫혀 전송할 수 없습니다.", isRequestSent: false, innerException: exception);
        }
        catch (Exception exception)
        {
            bool wasCurrent = AbortSocketIfCurrent(activeSocket);
            TransportFailure failure = wasCurrent ? TransportFailure.SendFailed : TransportFailure.ConnectionClosed;
            throw new TransportException(failure, "WebSocket 메시지 전송 중 오류가 발생했습니다.", isRequestSent: true, innerException: exception);
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
        ClientWebSocket? activeSocket = null;
        try
        {
            activeSocket = EnsureOpenAndGetSocket();
            return await ReceiveFullMessageAsync(activeSocket, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AbortSocketIfCurrent(activeSocket);
            throw;
        }
        catch (OperationCanceledException exception)
        {
            AbortSocketIfCurrent(activeSocket);
            throw new TransportException(TransportFailure.ConnectionClosed, "WebSocket 연결이 닫혀 수신할 수 없습니다.", isRequestSent: false, innerException: exception);
        }
        catch (TransportException)
        {
            AbortSocketIfCurrent(activeSocket);
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            AbortSocketIfCurrent(activeSocket);
            throw new TransportException(TransportFailure.ConnectionClosed, "WebSocket 연결이 닫혀 수신할 수 없습니다.", isRequestSent: false, innerException: exception);
        }
        catch (Exception exception)
        {
            bool wasCurrent = AbortSocketIfCurrent(activeSocket);
            TransportFailure failure = wasCurrent ? TransportFailure.ReceiveFailed : TransportFailure.ConnectionClosed;
            throw new TransportException(failure, "WebSocket 메시지 수신 중 오류가 발생했습니다.", isRequestSent: false, innerException: exception);
        }
        finally
        {
            _receiveLock.Release();
        }
    }

    private static WebSocketMessageType MapMessageType(WebSocketMessageKind kind) =>
        kind switch
        {
            WebSocketMessageKind.Text => WebSocketMessageType.Text,
            WebSocketMessageKind.Binary => WebSocketMessageType.Binary,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "지원하지 않는 WebSocket 메시지 형식입니다.")
        };

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
                AbortSocketIfCurrent(socket);
                throw new TransportException(TransportFailure.ConnectionClosed, "원격 호스트에서 WebSocket 연결을 닫았습니다.", isRequestSent: false);
            }

            messageKind ??= result.MessageType == WebSocketMessageType.Text ? WebSocketMessageKind.Text : WebSocketMessageKind.Binary;
            AppendFragmentOrThrow(socket, accumulator, chunk, result.Count);

            if (result.EndOfMessage)
            {
                return new WebSocketFrameMessage(messageKind.Value, accumulator.ToArray());
            }
        }
    }

    private void AppendFragmentOrThrow(ClientWebSocket socket, MemoryStream accumulator, byte[] chunk, int count)
    {
        if (accumulator.Length + count > _options.MaximumMessageSize)
        {
            AbortSocketIfCurrent(socket);
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
