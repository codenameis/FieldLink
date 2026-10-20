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
        private set
        {
            lock (_stateLock)
            {
                _state = value;
            }
        }
    }

    /// <inheritdoc />
    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_stateLock)
        {
            if (_state != TransportState.Closed)
            {
                throw new InvalidOperationException($"현재 상태({_state})에서는 소켓을 열 수 없습니다.");
            }

            _state = TransportState.Opening;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var client = _options.LocalPort.HasValue
                ? new UdpClient(_options.LocalPort.Value)
                : new UdpClient();

            client.Connect(_options.Host, _options.Port);

            _client = client;
            Interlocked.Increment(ref _connectionId);
            State = TransportState.Open;
            return Task.CompletedTask;
        }
        catch (OperationCanceledException)
        {
            State = TransportState.Closed;
            throw;
        }
        catch (Exception exception)
        {
            State = TransportState.Closed;
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
        lock (_stateLock)
        {
            if (_state is TransportState.Closed or TransportState.Disposed)
            {
                return Task.CompletedTask;
            }
        }

        CleanupClient();
        State = TransportState.Closed;
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

        ThrowIfDisposed();
        EnsureConnected();

        await _exclusiveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureConnected();
            var session = new UdpTransportSession(_connectionId, _client!, cancellationToken);
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
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                return;
            }

            _state = TransportState.Disposed;
        }

        CleanupClient();
        _exclusiveLock.Dispose();
    }

    private void EnsureConnected()
    {
        if (State != TransportState.Open || _client is null)
        {
            throw new InvalidOperationException("UDP 전송이 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void CleanupClient()
    {
        try
        {
            _client?.Close();
            _client?.Dispose();
        }
        catch
        {
            // 리소스 정리 중 예외는 무시
        }

        _client = null;
    }

    private void ThrowIfDisposed()
    {
        if (State == TransportState.Disposed)
        {
            throw new ObjectDisposedException(nameof(UdpTransport));
        }
    }

    private sealed class UdpTransportSession : ITransportSession
    {
        private readonly UdpClient _client;

        public UdpTransportSession(long connectionId, UdpClient client, CancellationToken cancellationToken)
        {
            ConnectionId = connectionId;
            _client = client;
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

            try
            {
                await _client.SendAsync(frame, frame.Length).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
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

            UdpReceiveResult receiveResult;
            try
            {
                Task<UdpReceiveResult> receiveTask = _client.ReceiveAsync();
                var tcs = new TaskCompletionSource<bool>();

                using (CancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), tcs))
                {
                    Task completedTask = await Task.WhenAny(receiveTask, tcs.Task).ConfigureAwait(false);
                    if (completedTask != receiveTask)
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                    }
                }

                receiveResult = await receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new TransportException(
                    TransportFailure.ReceiveFailed,
                    "UDP 데이터그램 수신에 실패했습니다.",
                    isRequestSent: true,
                    innerException: exception);
            }

            byte[] buffer = receiveResult.Buffer;
            if (!decoder.TryGetFrameLength(new ArraySegment<byte>(buffer), out int frameLength))
            {
                throw new TransportException(
                    TransportFailure.InvalidFrame,
                    "수신된 UDP 데이터그램이 완전한 프레임 조건을 만족하지 못했습니다.",
                    isRequestSent: true);
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
