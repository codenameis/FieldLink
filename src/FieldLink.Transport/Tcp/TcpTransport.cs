using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Tcp;

/// <summary>TCP 소켓을 통해 산업용 장치와 바이트를 주고받는 전송 구현체입니다.</summary>
public sealed class TcpTransport : ITransport
{
    private const int DrainChunkByteSize = 512;

    private readonly TcpTransportOptions _options;
    private readonly SemaphoreSlim _exclusiveLock = new(1, 1);
    private readonly object _stateLock = new();
    private readonly byte[] _drainScratchBuffer = new byte[DrainChunkByteSize];

    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamTransportSession? _session;
    private long _connectionId;
    private TransportState _state = TransportState.Closed;

    /// <summary>TCP 설정으로 전송 인스턴스를 초기화합니다.</summary>
    /// <param name="options">호스트, 포트, 시간 제한 등이 포함된 설정입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>가 null일 때.</exception>
    public TcpTransport(TcpTransportOptions options)
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
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_stateLock)
        {
            if (_state != TransportState.Closed)
            {
                throw new InvalidOperationException($"현재 상태({_state})에서는 연결을 열 수 없습니다.");
            }

            _state = TransportState.Opening;
        }

        try
        {
            var client = new TcpClient
            {
                NoDelay = _options.NoDelay,
                ReceiveBufferSize = _options.ReceiveBufferSize,
                SendBufferSize = _options.SendBufferSize
            };

            await ConnectWithTimeoutAsync(client, cancellationToken).ConfigureAwait(false);

            NetworkStream stream = client.GetStream();
            long connectionId = Interlocked.Increment(ref _connectionId);
            _client = client;
            _stream = stream;
            _session = new StreamTransportSession(
                connectionId,
                stream,
                CancellationToken.None,
                _options.ReceiveBufferSize,
                DrainPendingReceiveBytes);
            State = TransportState.Open;
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
                $"호스트 {_options.Host}:{_options.Port} 연결에 실패했습니다.",
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

        CleanupSocket();
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
            StreamTransportSession session = _session!;
            session.SetCancellationToken(cancellationToken);
            session.ClearReceiveBuffer();
            DrainPendingReceiveBytes();

            try
            {
                return await work(session).ConfigureAwait(false);
            }
            catch
            {
                session.ClearReceiveBuffer();
                DrainPendingReceiveBytes();
                throw;
            }
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

        CleanupSocket();
        _exclusiveLock.Dispose();
    }

    private async Task ConnectWithTimeoutAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(_options.ConnectTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        Task connectTask = client.ConnectAsync(_options.Host, _options.Port);
        var tcs = new TaskCompletionSource<bool>();

        using (linkedCts.Token.Register(state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), tcs))
        {
            Task completedTask = await Task.WhenAny(connectTask, tcs.Task).ConfigureAwait(false);

            if (completedTask != connectTask)
            {
                client.Close();
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                throw new TimeoutException($"TCP 연결 제한 시간({_options.ConnectTimeout.TotalMilliseconds}ms)이 초과되었습니다.");
            }
        }

        await connectTask.ConfigureAwait(false);
    }

    private void DrainPendingReceiveBytes()
    {
        NetworkStream? stream = _stream;
        if (stream is null)
        {
            return;
        }

        try
        {
            while (stream.DataAvailable)
            {
                int read = stream.Read(_drainScratchBuffer, 0, _drainScratchBuffer.Length);
                if (read <= 0)
                {
                    break;
                }
            }
        }
        catch
        {
            // 소켓 상태 이상 시 다음 송수신에서 정식 TransportException으로 보고됨
        }
    }

    private void EnsureConnected()
    {
        if (State != TransportState.Open || _stream is null || _session is null)
        {
            throw new InvalidOperationException("TCP 전송이 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void CleanupSocket()
    {
        _session?.ClearReceiveBuffer();
        _session = null;

        try
        {
            _stream?.Dispose();
        }
        catch
        {
            // 리소스 정리 중 예외는 무시
        }

        try
        {
            _client?.Close();
        }
        catch
        {
            // 리소스 정리 중 예외는 무시
        }

        _stream = null;
        _client = null;
    }

    private void ThrowIfDisposed()
    {
        if (State == TransportState.Disposed)
        {
            throw new ObjectDisposedException(nameof(TcpTransport));
        }
    }
}
