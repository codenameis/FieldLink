using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Serial;

/// <summary>RS-232/RS-422/RS-485 시리얼 포트를 통해 산업용 장치와 바이트를 주고받는 전송 구현체입니다.</summary>
public sealed class SerialTransport : ITransport
{
    private readonly SerialTransportOptions _options;
    private readonly SemaphoreSlim _exclusiveLock = new(1, 1);
    private readonly object _stateLock = new();

    private SerialPort? _serialPort;
    private long _connectionId;
    private TransportState _state = TransportState.Closed;

    /// <summary>시리얼 포트 설정으로 전송 인스턴스를 초기화합니다.</summary>
    /// <param name="options">포트명, 보율, 패리티 등이 포함된 설정입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>가 null일 때.</exception>
    public SerialTransport(SerialTransportOptions options)
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
                throw new InvalidOperationException($"현재 상태({_state})에서는 포트를 열 수 없습니다.");
            }

            _state = TransportState.Opening;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var port = new SerialPort(
                _options.PortName,
                _options.BaudRate,
                _options.Parity,
                _options.DataBits,
                _options.StopBits)
            {
                ReadTimeout = (int)_options.ReadTimeout.TotalMilliseconds,
                WriteTimeout = (int)_options.WriteTimeout.TotalMilliseconds
            };

            port.Open();

            _serialPort = port;
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
                $"시리얼 포트({_options.PortName})를 여는 데 실패했습니다.",
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

        CleanupPort();
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
            var session = new StreamTransportSession(
                _connectionId,
                _serialPort!.BaseStream,
                cancellationToken);

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

        CleanupPort();
        _exclusiveLock.Dispose();
    }

    private void EnsureConnected()
    {
        if (State != TransportState.Open || _serialPort is null || !_serialPort.IsOpen)
        {
            throw new InvalidOperationException("시리얼 포트 전송이 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void CleanupPort()
    {
        try
        {
            if (_serialPort is { IsOpen: true })
            {
                _serialPort.Close();
            }
            _serialPort?.Dispose();
        }
        catch
        {
            // 리소스 정리 중 예외는 무시
        }

        _serialPort = null;
    }

    private void ThrowIfDisposed()
    {
        if (State == TransportState.Disposed)
        {
            throw new ObjectDisposedException(nameof(SerialTransport));
        }
    }
}
