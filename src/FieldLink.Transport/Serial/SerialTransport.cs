using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Serial;

/// <summary>RS-232/RS-422/RS-485 시리얼 포트를 통해 산업용 장치와 바이트를 주고받는 전송 구현체입니다.</summary>
public sealed partial class SerialTransport : ITransport
{
    private readonly SerialTransportOptions _options;
    private readonly SemaphoreSlim _exclusiveLock = new(1, 1);
    private readonly object _stateLock = new();

    private SerialPort? _serialPort;
    private StreamTransportSession? _session;
    private CancellationTokenSource? _openCancellationSource;
    private long _connectionId;
    private long _openGeneration;
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
            (StreamTransportSession session, SerialPort port) = CaptureOpenSession();
            session.BeginExclusiveExchange(cancellationToken);
            session.ClearReceiveBuffer();
            DiscardSerialInBuffer(port);

            TResult result;
            try
            {
                result = await work(session).ConfigureAwait(false);
            }
            catch
            {
                session.ClearReceiveBuffer();
                DiscardSerialInBuffer(port);
                throw;
            }

            if (_options.SilenceInterval > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(_options.SilenceInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // 이미 완료된 송수신 결과를 후속 묵음 대기 중 취소로 실패 처리하지 않는다.
                }
            }

            return result;
        }
        finally
        {
            _exclusiveLock.Release();
        }
    }

    private (StreamTransportSession Session, SerialPort Port) CaptureOpenSession()
    {
        lock (_stateLock)
        {
            if (_state == TransportState.Disposed)
            {
                throw new ObjectDisposedException(nameof(SerialTransport));
            }

            if (_state != TransportState.Open || _serialPort is null || !_serialPort.IsOpen || _session is null)
            {
                throw new InvalidOperationException("시리얼 포트 전송이 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
            }

            return (_session, _serialPort);
        }
    }

    private static void DiscardSerialInBuffer(SerialPort? port)
    {
        if (port is not { IsOpen: true })
        {
            return;
        }

        try
        {
            port.DiscardInBuffer();
        }
        catch
        {
            // 포트 버퍼 비우기 중 발생한 부수적 예외는 무시
        }
    }
}
