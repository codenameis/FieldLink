using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Serial;

public sealed partial class SerialTransport
{
    /// <inheritdoc />
    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        long openGeneration = 0;
        CancellationTokenSource? openCancellationSource = null;
        SerialPort? port = null;
        try
        {
            (openGeneration, openCancellationSource) = BeginOpen(cancellationToken);
            openCancellationSource.Token.ThrowIfCancellationRequested();
            port = CreateAndOpenPort();
            CompleteOpen(port, openGeneration);
            port = null;
            return Task.CompletedTask;
        }
        catch (OperationCanceledException exception)
        {
            DisposePortSafely(port);
            if (openGeneration != 0)
            {
                ResetOpeningToClosed(openGeneration);
            }

            CancellationToken effectiveToken = exception.CancellationToken.IsCancellationRequested
                ? exception.CancellationToken
                : cancellationToken;
            return effectiveToken.IsCancellationRequested
                ? TransportTaskWait.CreateCanceledTask(effectiveToken)
                : Task.FromException(exception);
        }
        catch (ObjectDisposedException exception)
        {
            DisposePortSafely(port);
            if (openGeneration != 0)
            {
                ResetOpeningToClosed(openGeneration);
            }

            return Task.FromException(exception);
        }
        catch (InvalidOperationException exception) when (openGeneration == 0)
        {
            return Task.FromException(exception);
        }
        catch (Exception exception)
        {
            DisposePortSafely(port);
            if (openGeneration != 0)
            {
                ResetOpeningToClosed(openGeneration);
            }

            return Task.FromException(new TransportException(
                TransportFailure.ConnectFailed,
                $"시리얼 포트({_options.PortName})를 여는 데 실패했습니다.",
                isRequestSent: false,
                innerException: exception));
        }
        finally
        {
            if (openCancellationSource is not null)
            {
                ReleaseOpenCancellationSource(openCancellationSource);
            }
        }
    }

    /// <inheritdoc />
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        SerialPort? port;
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
            port = _serialPort;
            _serialPort = null;
            _session = null;
        }

        CancelSafely(openToCancel);
        DisposePortSafely(port);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SerialPort? port;
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
            port = _serialPort;
            _serialPort = null;
            _session = null;
        }

        CancelSafely(openToCancel);
        DisposePortSafely(port);
    }

    private (long OpenGeneration, CancellationTokenSource OpenCancellationSource) BeginOpen(CancellationToken callerToken)
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

            callerToken.ThrowIfCancellationRequested();

            _state = TransportState.Opening;
            _openGeneration++;
            CancellationTokenSource openCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            _openCancellationSource = openCancellationSource;
            return (_openGeneration, openCancellationSource);
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
            ReadTimeout = TransportTimeoutValidation.ToPositiveMillisecondsCeiling(_options.ReadTimeout),
            WriteTimeout = TransportTimeoutValidation.ToPositiveMillisecondsCeiling(_options.WriteTimeout)
        };

        port.Open();
        return port;
    }

    private void CompleteOpen(SerialPort port, long expectedOpenGeneration)
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(SerialTransport));
            }

            if (_state != TransportState.Opening || _openGeneration != expectedOpenGeneration)
            {
                throw new OperationCanceledException("포트 여는 중 전송이 닫혔습니다.");
            }

            long connectionId = Interlocked.Increment(ref _connectionId);
            _serialPort = port;
            _session = new StreamTransportSession(
                connectionId,
                port.BaseStream,
                CancellationToken.None,
                onBeforeSend: () => DiscardSerialInBuffer(port),
                onReadAbandoned: () => AbortConnectionIfCurrent(connectionId));
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

    private void AbortConnectionIfCurrent(long expectedConnectionId)
    {
        SerialPort? port;
        lock (_stateLock)
        {
            if (_state != TransportState.Open || Interlocked.Read(ref _connectionId) != expectedConnectionId)
            {
                return;
            }

            _state = TransportState.Closed;
            port = _serialPort;
            _serialPort = null;
            _session = null;
        }

        DisposePortSafely(port);
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
