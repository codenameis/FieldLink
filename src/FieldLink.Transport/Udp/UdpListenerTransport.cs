using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Udp;

/// <summary>로컬 UDP 포트에서 장치의 선행 송신 패킷을 수신하고 원격 엔드포인트로 응답하는 전송 구현체입니다.</summary>
public sealed class UdpListenerTransport : IUdpListenerTransport
{
    private readonly UdpListenerTransportOptions _options;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _stateLock = new();

    private UdpClient? _client;
    private Task<UdpReceiveResult>? _pendingReceiveTask;
    private int _boundPort;
    private TransportState _state = TransportState.Closed;

    /// <summary>로컬 바인딩 설정으로 UDP 리스너 전송을 초기화합니다.</summary>
    /// <param name="options">로컬 포트 및 IP 설정입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>가 null일 때.</exception>
    public UdpListenerTransport(UdpListenerTransportOptions options)
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
    public int BoundPort
    {
        get
        {
            lock (_stateLock)
            {
                return _boundPort;
            }
        }
    }

    /// <inheritdoc />
    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        BeginOpen();

        UdpClient? client = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localEndPoint = new IPEndPoint(_options.LocalAddress, _options.LocalPort);
            client = new UdpClient(localEndPoint);
            int actualPort = ((IPEndPoint)client.Client.LocalEndPoint).Port;
            CompleteOpen(client, actualPort);
            client = null;
            return Task.CompletedTask;
        }
        catch (OperationCanceledException)
        {
            DisposeClientSafely(client);
            ResetOpeningToClosed();
            throw;
        }
        catch (ObjectDisposedException)
        {
            DisposeClientSafely(client);
            throw;
        }
        catch (Exception exception)
        {
            DisposeClientSafely(client);
            ResetOpeningToClosed();
            throw new TransportException(
                TransportFailure.ConnectFailed,
                $"로컬 UDP 포트({_options.LocalAddress}:{_options.LocalPort}) 바인딩에 실패했습니다.",
                isRequestSent: false,
                innerException: exception);
        }
    }

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        UdpClient? client;
        Task<UdpReceiveResult>? pendingReceive;
        lock (_stateLock)
        {
            if (_state is TransportState.Closed or TransportState.Disposed)
            {
                return Task.CompletedTask;
            }

            _state = TransportState.Closed;
            _boundPort = 0;
            client = _client;
            pendingReceive = _pendingReceiveTask;
            _client = null;
            _pendingReceiveTask = null;
        }

        ObserveFaultSafely(pendingReceive);
        DisposeClientSafely(client);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<UdpDatagram> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        UdpClient client = EnsureOpenAndGetClient();
        Task<UdpReceiveResult> receiveTask = GetOrStartReceiveTask(client);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            using (cancellationToken.Register(
                static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                tcs))
            {
                Task completed = await Task.WhenAny(receiveTask, tcs.Task).ConfigureAwait(false);
                if (completed != receiveTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            ClearPendingReceiveTask(receiveTask);
            UdpReceiveResult result = await receiveTask.ConfigureAwait(false);
            return new UdpDatagram(result.Buffer, result.RemoteEndPoint);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            ClearPendingReceiveTask(receiveTask);
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "UDP 리스너 소켓이 닫혀 수신할 수 없습니다.",
                isRequestSent: false,
                innerException: exception);
        }
        catch (Exception exception)
        {
            if (receiveTask.IsCompleted)
            {
                ClearPendingReceiveTask(receiveTask);
            }

            throw new TransportException(
                TransportFailure.ReceiveFailed,
                "UDP 데이터그램 수신에 실패했습니다.",
                isRequestSent: false,
                innerException: exception);
        }
    }

    /// <inheritdoc />
    public async Task SendToAsync(byte[] payload, IPEndPoint remoteEndPoint, CancellationToken cancellationToken = default)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (remoteEndPoint is null)
        {
            throw new ArgumentNullException(nameof(remoteEndPoint));
        }

        _ = EnsureOpenAndGetClient();

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            UdpClient client = EnsureOpenAndGetClient();
            cancellationToken.ThrowIfCancellationRequested();
            await client.SendAsync(payload, payload.Length, remoteEndPoint).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "UDP 리스너 소켓이 닫혀 전송할 수 없습니다.",
                isRequestSent: false,
                innerException: exception);
        }
        catch (Exception exception)
        {
            throw new TransportException(
                TransportFailure.SendFailed,
                $"원격 엔드포인트({remoteEndPoint})로 UDP 데이터그램 전송에 실패했습니다.",
                isRequestSent: true,
                innerException: exception);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        UdpClient? client;
        Task<UdpReceiveResult>? pendingReceive;
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                return;
            }

            _state = TransportState.Disposed;
            _boundPort = 0;
            client = _client;
            pendingReceive = _pendingReceiveTask;
            _client = null;
            _pendingReceiveTask = null;
        }

        ObserveFaultSafely(pendingReceive);
        DisposeClientSafely(client);
    }

    private void BeginOpen()
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

            _state = TransportState.Opening;
        }
    }

    private void CompleteOpen(UdpClient client, int actualPort)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(UdpListenerTransport));
            }

            if (_state != TransportState.Opening)
            {
                throw new OperationCanceledException("UDP 리스너 여는 중 닫혔습니다.");
            }

            _client = client;
            _pendingReceiveTask = null;
            _boundPort = actualPort;
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

    private UdpClient EnsureOpenAndGetClient()
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(UdpListenerTransport));
            }

            if (_state != TransportState.Open || _client is null)
            {
                throw new InvalidOperationException("UDP 리스너가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
            }

            return _client;
        }
    }

    private Task<UdpReceiveResult> GetOrStartReceiveTask(UdpClient client)
    {
        lock (_stateLock)
        {
            return _pendingReceiveTask ??= client.ReceiveAsync();
        }
    }

    private void ClearPendingReceiveTask(Task<UdpReceiveResult> completedTask)
    {
        lock (_stateLock)
        {
            if (ReferenceEquals(_pendingReceiveTask, completedTask))
            {
                _pendingReceiveTask = null;
            }
        }
    }

    private static void ObserveFaultSafely(Task? task)
    {
        if (task is null)
        {
            return;
        }

        _ = task.ContinueWith(
            static faulted => _ = faulted.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
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
