using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Udp;

/// <summary>로컬 UDP 포트에서 장치의 선행 송신 패킷을 수신하고 원격 엔드포인트로 응답하는 전송 구현체입니다.</summary>
public sealed partial class UdpListenerTransport : IUdpListenerTransport
{
    private readonly UdpListenerTransportOptions _options;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _receiveLock = new(1, 1);
    private readonly object _stateLock = new();

    private UdpClient? _client;
    private Task<UdpReceiveResult>? _pendingReceiveTask;
    private CancellationTokenSource? _openCancellationSource;
    private int _boundPort;
    private long _openGeneration;
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
    public async Task<UdpDatagram> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        _ = EnsureOpenAndGetClient();

        await _receiveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            UdpClient client = EnsureOpenAndGetClient();
            Task<UdpReceiveResult> receiveTask = GetOrStartReceiveTask(client);
            var cancelCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                using (cancellationToken.Register(
                    static state =>
                    {
                        if (state is TaskCompletionSource<bool> completion)
                        {
                            completion.TrySetResult(true);
                        }
                    },
                    cancelCompletion))
                {
                    Task completed = await Task.WhenAny(receiveTask, cancelCompletion.Task).ConfigureAwait(false);
                    if (completed != receiveTask)
                    {
                        TransportTaskWait.ObserveFaultSafely(receiveTask);
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
            catch (TransportException)
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
        finally
        {
            _receiveLock.Release();
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
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(UdpListenerTransport));
            }

            if (_state != TransportState.Open || !ReferenceEquals(_client, client))
            {
                throw new TransportException(
                    TransportFailure.ConnectionClosed,
                    "UDP 리스너 소켓이 닫혀 수신할 수 없습니다.",
                    isRequestSent: false);
            }

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
}
