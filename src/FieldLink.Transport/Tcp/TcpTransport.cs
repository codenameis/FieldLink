using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Tcp;

/// <summary>TCP 소켓을 통해 산업용 장치와 바이트를 주고받는 전송 구현체입니다.</summary>
public sealed class TcpTransport : ITransport
{
    private readonly TcpTransportOptions _options;
    private readonly SemaphoreSlim _exclusiveLock = new(1, 1);
    private readonly object _stateLock = new();

    private TcpClient? _client;
    private NetworkStream? _stream;
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
        BeginOpen();

        TcpClient? client = null;
        try
        {
            client = CreateConfiguredClient();
            await ConnectWithTimeoutAsync(client, cancellationToken).ConfigureAwait(false);
            CompleteOpen(client);
            client = null;
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
                $"호스트 {_options.Host}:{_options.Port} 연결에 실패했습니다.",
                isRequestSent: false,
                innerException: exception);
        }
    }

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        TcpClient? client;
        NetworkStream? stream;
        lock (_stateLock)
        {
            if (_state is TransportState.Closed or TransportState.Disposed)
            {
                return Task.CompletedTask;
            }

            _state = TransportState.Closed;
            client = _client;
            stream = _stream;
            _client = null;
            _stream = null;
        }

        DisposeResourcesSafely(stream, client);
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

        _ = CaptureOpenSession();

        await _exclusiveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (long connectionId, NetworkStream stream) = CaptureOpenSession();
            var session = new StreamTransportSession(
                connectionId,
                stream,
                cancellationToken,
                _options.ReceiveBufferSize);

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
        TcpClient? client;
        NetworkStream? stream;
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                return;
            }

            _state = TransportState.Disposed;
            client = _client;
            stream = _stream;
            _client = null;
            _stream = null;
        }

        DisposeResourcesSafely(stream, client);
    }

    private void BeginOpen()
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(TcpTransport));
            }

            if (_state != TransportState.Closed)
            {
                throw new InvalidOperationException($"현재 상태({_state})에서는 연결을 열 수 없습니다.");
            }

            _state = TransportState.Opening;
        }
    }

    private TcpClient CreateConfiguredClient() =>
        new()
        {
            NoDelay = _options.NoDelay,
            ReceiveBufferSize = _options.ReceiveBufferSize,
            SendBufferSize = _options.SendBufferSize
        };

    private void CompleteOpen(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(TcpTransport));
            }

            if (_state != TransportState.Opening)
            {
                throw new OperationCanceledException("연결 시도 중 전송이 닫혔습니다.");
            }

            _client = client;
            _stream = stream;
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

    private (long ConnectionId, NetworkStream Stream) CaptureOpenSession()
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(TcpTransport));
            }

            if (_state != TransportState.Open || _stream is null)
            {
                throw new InvalidOperationException("TCP 전송이 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
            }

            return (Interlocked.Read(ref _connectionId), _stream);
        }
    }

    private async Task ConnectWithTimeoutAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(_options.ConnectTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        Task connectTask = client.ConnectAsync(_options.Host, _options.Port);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using (linkedCts.Token.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
            tcs))
        {
            Task completedTask = await Task.WhenAny(connectTask, tcs.Task).ConfigureAwait(false);
            if (completedTask != connectTask)
            {
                _ = connectTask.ContinueWith(
                    static faulted => _ = faulted.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
                client.Close();
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"TCP 연결 제한 시간({_options.ConnectTimeout.TotalMilliseconds}ms)이 초과되었습니다.");
            }
        }

        await connectTask.ConfigureAwait(false);
    }

    private static void DisposeClientSafely(TcpClient? client) => DisposeResourcesSafely(null, client);

    private static void DisposeResourcesSafely(NetworkStream? stream, TcpClient? client)
    {
        try
        {
            stream?.Dispose();
        }
        catch
        {
            // 리소스 정리 중 예외는 무시
        }

        try
        {
            client?.Close();
        }
        catch
        {
            // 리소스 정리 중 예외는 무시
        }
    }
}
