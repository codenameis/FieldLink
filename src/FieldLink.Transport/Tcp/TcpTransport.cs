using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Tcp;

/// <summary>TCP 소켓을 통해 산업용 장치와 바이트를 주고받는 전송 구현체입니다.</summary>
public sealed partial class TcpTransport : ITransport
{
    private const int DrainChunkByteSize = 512;
    private const int MaximumDrainBytes = 65536;

    private readonly TcpTransportOptions _options;
    private readonly SemaphoreSlim _exclusiveLock = new(1, 1);
    private readonly object _stateLock = new();
    private readonly byte[] _drainScratchBuffer = new byte[DrainChunkByteSize];

    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamTransportSession? _session;
    private CancellationTokenSource? _openCancellationSource;
    private long _connectionId;
    private long _openGeneration;
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
            (StreamTransportSession session, NetworkStream stream) = CaptureOpenSession();
            session.BeginExclusiveExchange(cancellationToken);
            session.ClearReceiveBuffer();
            DrainPendingReceiveBytes(stream);

            try
            {
                return await work(session).ConfigureAwait(false);
            }
            catch
            {
                session.ClearReceiveBuffer();
                DrainPendingReceiveBytes(stream);
                throw;
            }
        }
        finally
        {
            _exclusiveLock.Release();
        }
    }

    private (StreamTransportSession Session, NetworkStream Stream) CaptureOpenSession()
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(TcpTransport));
            }

            if (_state != TransportState.Open || _stream is null || _session is null)
            {
                throw new InvalidOperationException("TCP 전송이 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
            }

            return (_session, _stream);
        }
    }

    private void DrainPendingReceiveBytes(NetworkStream? stream)
    {
        if (stream is null)
        {
            return;
        }

        try
        {
            int totalDrainedBytes = 0;
            while (totalDrainedBytes < MaximumDrainBytes && stream.DataAvailable)
            {
                int bytesToRead = Math.Min(_drainScratchBuffer.Length, MaximumDrainBytes - totalDrainedBytes);
                int bytesRead = stream.Read(_drainScratchBuffer, 0, bytesToRead);
                if (bytesRead <= 0)
                {
                    break;
                }

                totalDrainedBytes += bytesRead;
            }
        }
        catch
        {
            // 소켓 상태 이상 시 다음 송수신에서 정식 TransportException으로 보고됨
        }
    }

    private async Task ConnectWithTimeoutAsync(
        TcpClient client,
        CancellationToken openToken,
        CancellationToken callerToken)
    {
        using CancellationTokenSource timeoutSource = new(_options.ConnectTimeout);
        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            openToken,
            timeoutSource.Token);

        Task connectTask = client.ConnectAsync(_options.Host, _options.Port);
        var cancelCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using (linkedSource.Token.Register(
            static state =>
            {
                if (state is TaskCompletionSource<bool> completion)
                {
                    completion.TrySetResult(true);
                }
            },
            cancelCompletion))
        {
            Task completedTask = await Task.WhenAny(connectTask, cancelCompletion.Task).ConfigureAwait(false);
            if (completedTask != connectTask)
            {
                TransportTaskWait.ObserveFaultSafely(connectTask);
                DisposeClientSafely(client);
                callerToken.ThrowIfCancellationRequested();
                if (openToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("연결 시도 중 전송이 닫혔습니다.", openToken);
                }

                throw new TimeoutException($"TCP 연결 제한 시간({_options.ConnectTimeout.TotalMilliseconds}ms)이 초과되었습니다.");
            }
        }

        await connectTask.ConfigureAwait(false);
    }
}
