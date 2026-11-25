using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Tcp;

public sealed partial class TcpTransport
{
    /// <inheritdoc />
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        (long openGeneration, CancellationTokenSource openCancellationSource) = BeginOpen(cancellationToken);

        TcpClient? client = null;
        try
        {
            client = CreateConfiguredClient();
            await ConnectWithTimeoutAsync(client, openCancellationSource.Token, cancellationToken).ConfigureAwait(false);
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
                $"호스트 {_options.Host}:{_options.Port} 연결에 실패했습니다.",
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
        TcpClient? client;
        NetworkStream? stream;
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
            stream = _stream;
            _client = null;
            _stream = null;
            _session = null;
        }

        CancelSafely(openToCancel);
        DisposeResourcesSafely(stream, client);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        TcpClient? client;
        NetworkStream? stream;
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
            stream = _stream;
            _client = null;
            _stream = null;
            _session = null;
        }

        CancelSafely(openToCancel);
        DisposeResourcesSafely(stream, client);
    }

    private (long OpenGeneration, CancellationTokenSource OpenCancellationSource) BeginOpen(CancellationToken callerToken)
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

            callerToken.ThrowIfCancellationRequested();

            _state = TransportState.Opening;
            _openGeneration++;
            CancellationTokenSource openCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            _openCancellationSource = openCancellationSource;
            return (_openGeneration, openCancellationSource);
        }
    }

    private TcpClient CreateConfiguredClient() =>
        new()
        {
            NoDelay = _options.NoDelay,
            ReceiveBufferSize = _options.ReceiveBufferSize,
            SendBufferSize = _options.SendBufferSize
        };

    private void CompleteOpen(TcpClient client, long expectedOpenGeneration)
    {
        NetworkStream stream = client.GetStream();
        try
        {
            lock (_stateLock)
            {
                if (_state == TransportState.Disposed)
                {
                    throw new ObjectDisposedException(nameof(TcpTransport));
                }

                if (_state != TransportState.Opening || _openGeneration != expectedOpenGeneration)
                {
                    throw new OperationCanceledException("연결 시도 중 전송이 닫혔습니다.");
                }

                long connectionId = Interlocked.Increment(ref _connectionId);
                _client = client;
                _stream = stream;
                _session = new StreamTransportSession(
                    connectionId,
                    stream,
                    CancellationToken.None,
                    _options.ReceiveBufferSize,
                    () => DrainPendingReceiveBytes(stream),
                    () => AbortConnectionIfCurrent(connectionId));
                _state = TransportState.Open;
            }
        }
        catch
        {
            DisposeResourcesSafely(stream, null);
            throw;
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

    private void AbortConnectionIfCurrent(long expectedConnectionId)
    {
        TcpClient? client;
        NetworkStream? stream;
        lock (_stateLock)
        {
            if (_state != TransportState.Open || Interlocked.Read(ref _connectionId) != expectedConnectionId)
            {
                return;
            }

            _state = TransportState.Closed;
            client = _client;
            stream = _stream;
            _client = null;
            _stream = null;
            _session = null;
        }

        DisposeResourcesSafely(stream, client);
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
