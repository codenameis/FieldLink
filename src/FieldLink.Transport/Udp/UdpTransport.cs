using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Framing;

namespace FieldLink.Transport.Udp;

/// <summary>UDP 소켓을 통해 산업용 장치와 바이트를 주고받는 전송 구현체입니다.</summary>
public sealed class UdpTransport : ITransport
{
    private readonly UdpTransportOptions _options;
    private readonly SemaphoreSlim _exclusiveLock = new(1, 1);
    private readonly object _stateLock = new();

    private UdpClient? _client;
    private Task<UdpReceiveResult>? _pendingReceiveTask;
    private long _connectionId;
    private TransportState _state = TransportState.Closed;

    /// <summary>UDP 설정으로 전송 인스턴스를 초기화합니다.</summary>
    /// <param name="options">호스트, 포트 등이 포함된 설정입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>가 null일 때.</exception>
    public UdpTransport(UdpTransportOptions options)
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
    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        BeginOpen();

        UdpClient? client = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            client = CreateAndConnectClient();
            CompleteOpen(client);
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
                $"UDP 소켓 초기화에 실패했습니다. (대상: {_options.Host}:{_options.Port})",
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
    public async Task<TResult> RunExclusiveAsync<TResult>(
        Func<ITransportSession, Task<TResult>> work,
        CancellationToken cancellationToken = default)
    {
        if (work is null)
        {
            throw new ArgumentNullException(nameof(work));
        }

        _ = CaptureOpenClient();

        await _exclusiveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (long connectionId, UdpClient client) = CaptureOpenClient();
            var session = new UdpTransportSession(this, connectionId, client, _options.ReceiveTimeout, cancellationToken);
            return await work(session).ConfigureAwait(false);
        }
        finally
        {
            _exclusiveLock.Release();
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
                throw new ObjectDisposedException(nameof(UdpTransport));
            }

            if (_state != TransportState.Closed)
            {
                throw new InvalidOperationException($"현재 상태({_state})에서는 소켓을 열 수 없습니다.");
            }

            _state = TransportState.Opening;
        }
    }

    private UdpClient CreateAndConnectClient()
    {
        var client = _options.LocalPort.HasValue
            ? new UdpClient(_options.LocalPort.Value)
            : new UdpClient();
        client.Client.ReceiveTimeout = (int)_options.ReceiveTimeout.TotalMilliseconds;
        client.Connect(_options.Host, _options.Port);
        return client;
    }

    private void CompleteOpen(UdpClient client)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(UdpTransport));
            }

            if (_state != TransportState.Opening)
            {
                throw new OperationCanceledException("소켓 여는 중 전송이 닫혔습니다.");
            }

            _client = client;
            _pendingReceiveTask = null;
            Interlocked.Increment(ref _connectionId);
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

    private (long ConnectionId, UdpClient Client) CaptureOpenClient()
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(UdpTransport));
            }

            if (_state != TransportState.Open || _client is null)
            {
                throw new InvalidOperationException("UDP 전송이 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
            }

            return (Interlocked.Read(ref _connectionId), _client);
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
            // 리소스 정리 중 예외는 무시
        }
    }

    private sealed class UdpTransportSession : ITransportSession
    {
        private readonly UdpTransport _owner;
        private readonly UdpClient _client;
        private readonly TimeSpan _receiveTimeout;
        private bool _hasSent;

        public UdpTransportSession(
            UdpTransport owner,
            long connectionId,
            UdpClient client,
            TimeSpan receiveTimeout,
            CancellationToken cancellationToken)
        {
            _owner = owner;
            ConnectionId = connectionId;
            _client = client;
            _receiveTimeout = receiveTimeout;
            CancellationToken = cancellationToken;
        }

        public long ConnectionId { get; }

        public CancellationToken CancellationToken { get; }

        public async Task SendAsync(byte[] frame)
        {
            if (frame is null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            CancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _client.SendAsync(frame, frame.Length).ConfigureAwait(false);
                _hasSent = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ObjectDisposedException exception)
            {
                throw new TransportException(
                    TransportFailure.ConnectionClosed,
                    "UDP 소켓이 닫혀 전송할 수 없습니다.",
                    isRequestSent: false,
                    innerException: exception);
            }
            catch (Exception exception)
            {
                throw new TransportException(
                    TransportFailure.SendFailed,
                    "UDP 데이터그램 전송에 실패했습니다.",
                    isRequestSent: true,
                    innerException: exception);
            }
        }

        public async Task<byte[]> ReceiveAsync(IFrameDecoder decoder)
        {
            if (decoder is null)
            {
                throw new ArgumentNullException(nameof(decoder));
            }

            UdpReceiveResult receiveResult = await ReceiveDatagramAsync().ConfigureAwait(false);
            return DecodeDatagram(decoder, receiveResult.Buffer);
        }

        private async Task<UdpReceiveResult> ReceiveDatagramAsync()
        {
            using var timeoutCts = new CancellationTokenSource(_receiveTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, timeoutCts.Token);

            Task<UdpReceiveResult> receiveTask = _owner.GetOrStartReceiveTask(_client);
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                using (linkedCts.Token.Register(
                    static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                    tcs))
                {
                    Task completedTask = await Task.WhenAny(receiveTask, tcs.Task).ConfigureAwait(false);
                    if (completedTask != receiveTask)
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        throw new TimeoutException($"UDP 수신 제한 시간({_receiveTimeout.TotalMilliseconds}ms)이 초과되었습니다.");
                    }
                }

                _owner.ClearPendingReceiveTask(receiveTask);
                return await receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ObjectDisposedException exception)
            {
                _owner.ClearPendingReceiveTask(receiveTask);
                throw new TransportException(
                    TransportFailure.ConnectionClosed,
                    "UDP 소켓이 닫혀 수신할 수 없습니다.",
                    isRequestSent: _hasSent,
                    innerException: exception);
            }
            catch (Exception exception)
            {
                if (receiveTask.IsCompleted)
                {
                    _owner.ClearPendingReceiveTask(receiveTask);
                }

                throw new TransportException(
                    TransportFailure.ReceiveFailed,
                    "UDP 데이터그램 수신에 실패했습니다.",
                    isRequestSent: _hasSent,
                    innerException: exception);
            }
        }

        private byte[] DecodeDatagram(IFrameDecoder decoder, byte[] buffer)
        {
            if (!decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength))
            {
                throw new TransportException(
                    TransportFailure.InvalidFrame,
                    "수신된 UDP 데이터그램이 완전한 프레임 조건을 만족하지 못했습니다.",
                    isRequestSent: _hasSent);
            }

            if (frameLength <= 0 || frameLength > buffer.Length)
            {
                throw new TransportException(
                    TransportFailure.InvalidFrame,
                    $"디코더가 유효하지 않은 프레임 길이({frameLength})를 반환했습니다.",
                    isRequestSent: _hasSent);
            }

            if (frameLength == buffer.Length)
            {
                return buffer;
            }

            byte[] frame = new byte[frameLength];
            Array.Copy(buffer, 0, frame, 0, frameLength);
            return frame;
        }
    }
}
