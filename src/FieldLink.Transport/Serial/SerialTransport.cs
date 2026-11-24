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
        BeginOpen();

        SerialPort? port = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            port = CreateAndOpenPort();
            CompleteOpen(port);
            port = null;
            return Task.CompletedTask;
        }
        catch (OperationCanceledException)
        {
            DisposePortSafely(port);
            ResetOpeningToClosed();
            throw;
        }
        catch (ObjectDisposedException)
        {
            DisposePortSafely(port);
            throw;
        }
        catch (Exception exception)
        {
            DisposePortSafely(port);
            ResetOpeningToClosed();
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
        SerialPort? port;
        lock (_stateLock)
        {
            if (_state is TransportState.Closed or TransportState.Disposed)
            {
                return Task.CompletedTask;
            }

            _state = TransportState.Closed;
            port = _serialPort;
            _serialPort = null;
        }

        DisposePortSafely(port);
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

        _ = CaptureOpenPort();

        await _exclusiveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (long connectionId, SerialPort port) = CaptureOpenPort();
            var session = new StreamTransportSession(
                connectionId,
                port.BaseStream,
                cancellationToken);

            TResult result = await work(session).ConfigureAwait(false);
            if (_options.SilenceInterval > TimeSpan.Zero)
            {
                await Task.Delay(_options.SilenceInterval, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            _exclusiveLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SerialPort? port;
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                return;
            }

            _state = TransportState.Disposed;
            port = _serialPort;
            _serialPort = null;
        }

        DisposePortSafely(port);
    }

    private void BeginOpen()
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(SerialTransport));
            }

            if (_state != TransportState.Closed)
            {
                throw new InvalidOperationException($"현재 상태({_state})에서는 포트를 열 수 없습니다.");
            }

            _state = TransportState.Opening;
        }
    }

    private SerialPort CreateAndOpenPort()
    {
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
        return port;
    }

    private void CompleteOpen(SerialPort port)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(SerialTransport));
            }

            if (_state != TransportState.Opening)
            {
                throw new OperationCanceledException("포트 여는 중 전송이 닫혔습니다.");
            }

            _serialPort = port;
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

    private (long ConnectionId, SerialPort Port) CaptureOpenPort()
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(SerialTransport));
            }

            if (_state != TransportState.Open || _serialPort is null || !_serialPort.IsOpen)
            {
                throw new InvalidOperationException("시리얼 포트 전송이 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
            }

            return (Interlocked.Read(ref _connectionId), _serialPort);
        }
    }

    private static void DisposePortSafely(SerialPort? port)
    {
        try
        {
            if (port is { IsOpen: true })
            {
                port.Close();
            }

            port?.Dispose();
        }
        catch
        {
            // 리소스 정리 중 예외는 무시
        }
    }
}
