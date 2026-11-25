using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Udp;

public sealed partial class UdpTransport
{
    /// <inheritdoc />
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        (long openGeneration, CancellationTokenSource openCancellationSource) = BeginOpen(cancellationToken);

        UdpClient? client = null;
        try
        {
            client = await CreateAndConnectClientAsync(openCancellationSource.Token).ConfigureAwait(false);
            CompleteOpen(client, openGeneration);
            client = null;
        }
        catch (OperationCanceledException)
        {
            DisposeClientSafely(client);
            ResetOpeningToClosed(openGeneration);
            throw;
        }
        catch (ObjectDisposedException)
        {
            DisposeClientSafely(client);
            ResetOpeningToClosed(openGeneration);
            throw;
        }
        catch (Exception exception)
        {
            DisposeClientSafely(client);
            ResetOpeningToClosed(openGeneration);
            throw new TransportException(
                TransportFailure.ConnectFailed,
                $"UDP 소켓 초기화에 실패했습니다. (대상: {_options.Host}:{_options.Port})",
                isRequestSent: false,
                innerException: exception);
        }
        finally
        {
            ReleaseOpenCancellationSource(openCancellationSource);
        }
    }

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        UdpClient? client;
        Task<UdpReceiveResult>? pendingReceive;
        CancellationTokenSource? openToCancel;
        lock (_stateLock)
        {
            if (_state is TransportState.Closed or TransportState.Disposed)
            {
                return Task.CompletedTask;
            }

            _state = TransportState.Closed;
            _openGeneration++;
            openToCancel = _openCancellationSource;
            _openCancellationSource = null;
            client = _client;
            pendingReceive = _pendingReceiveTask;
            _client = null;
            _pendingReceiveTask = null;
        }

        CancelSafely(openToCancel);
        TransportTaskWait.ObserveFaultSafely(pendingReceive);
        DisposeClientSafely(client);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        UdpClient? client;
        Task<UdpReceiveResult>? pendingReceive;
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
            client = _client;
            pendingReceive = _pendingReceiveTask;
            _client = null;
            _pendingReceiveTask = null;
        }

        CancelSafely(openToCancel);
        TransportTaskWait.ObserveFaultSafely(pendingReceive);
        DisposeClientSafely(client);
    }

    internal Task<UdpReceiveResult> GetOrStartReceiveTask(UdpClient client, bool isRequestSent)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(UdpTransport));
            }

            if (_state != TransportState.Open || !ReferenceEquals(_client, client))
            {
                throw new TransportException(
                    TransportFailure.ConnectionClosed,
                    "UDP 소켓이 닫혀 수신할 수 없습니다.",
                    isRequestSent: isRequestSent);
            }

            return _pendingReceiveTask ??= client.ReceiveAsync();
        }
    }

    internal void ClearPendingReceiveTask(Task<UdpReceiveResult> completedTask)
    {
        lock (_stateLock)
        {
            if (ReferenceEquals(_pendingReceiveTask, completedTask))
            {
                _pendingReceiveTask = null;
            }
        }
    }

    internal void DiscardStalePendingReceiveAndDrain(UdpClient client)
    {
        Task<UdpReceiveResult>? staleTask = null;
        lock (_stateLock)
        {
            if (_state == TransportState.Open &&
                ReferenceEquals(_client, client) &&
                _pendingReceiveTask is { IsCompleted: true } completedTask)
            {
                staleTask = completedTask;
                _pendingReceiveTask = null;
            }
        }

        TransportTaskWait.ObserveFaultSafely(staleTask);

        try
        {
            int drainedDatagrams = 0;
            int drainedBytes = 0;
            while (drainedDatagrams < MaximumDrainDatagrams &&
                   drainedBytes < MaximumDrainBytes &&
                   client.Available > 0)
            {
                IPEndPoint remoteEndPoint = new(IPAddress.Any, 0);
                byte[] discarded = client.Receive(ref remoteEndPoint);
                drainedDatagrams++;
                drainedBytes += discarded.Length;
            }
        }
        catch
        {
            // 소켓 상태 이상 시 후속 송수신에서 정식 TransportException으로 보고됨
        }
    }

    private (long OpenGeneration, CancellationTokenSource OpenCancellationSource) BeginOpen(CancellationToken callerToken)
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

            callerToken.ThrowIfCancellationRequested();

            _state = TransportState.Opening;
            _openGeneration++;
            CancellationTokenSource openCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            _openCancellationSource = openCancellationSource;
            return (_openGeneration, openCancellationSource);
        }
    }

    private async Task<UdpClient> CreateAndConnectClientAsync(CancellationToken openToken)
    {
        IPAddress targetAddress = await ResolveTargetAddressAsync(_options.Host, openToken).ConfigureAwait(false);
        openToken.ThrowIfCancellationRequested();

        UdpClient client = _options.LocalPort.HasValue
            ? new UdpClient(_options.LocalPort.Value, targetAddress.AddressFamily)
            : new UdpClient(targetAddress.AddressFamily);

        try
        {
            client.Client.ReceiveTimeout = TransportTimeoutValidation.ToPositiveMillisecondsCeiling(_options.ReceiveTimeout);
            client.Connect(targetAddress, _options.Port);
            return client;
        }
        catch
        {
            DisposeClientSafely(client);
            throw;
        }
    }

    private static async Task<IPAddress> ResolveTargetAddressAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out IPAddress? parsedAddress))
        {
            return parsedAddress;
        }

        IPAddress[] addresses = await TransportTaskWait
            .WaitWithCancellationAsync(Dns.GetHostAddressesAsync(host), cancellationToken)
            .ConfigureAwait(false);

        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        for (int i = 0; i < addresses.Length; i++)
        {
            if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
            {
                return addresses[i];
            }
        }

        return addresses[0];
    }

    private void CompleteOpen(UdpClient client, long expectedOpenGeneration)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(UdpTransport));
            }

            if (_state != TransportState.Opening || _openGeneration != expectedOpenGeneration)
            {
                throw new OperationCanceledException("소켓 여는 중 전송이 닫혔습니다.");
            }

            _client = client;
            _pendingReceiveTask = null;
            Interlocked.Increment(ref _connectionId);
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

    private static void CancelSafely(CancellationTokenSource? cancellationSource)
    {
        if (cancellationSource is null)
        {
            return;
        }

        try
        {
            cancellationSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // OpenAsync finally와 경합하여 이미 해제된 경우 무시
        }
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
}
