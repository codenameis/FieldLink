using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport.Udp;

/// <summary>UDP 소켓을 통해 산업용 장치와 바이트를 주고받는 전송 구현체입니다.</summary>
public sealed partial class UdpTransport : ITransport
{
    private const int MaximumDrainDatagrams = 64;
    private const int MaximumDrainBytes = 65536;

    private readonly UdpTransportOptions _options;
    private readonly SemaphoreSlim _exclusiveLock = new(1, 1);
    private readonly object _stateLock = new();

    private UdpClient? _client;
    private Task<UdpReceiveResult>? _pendingReceiveTask;
    private CancellationTokenSource? _openCancellationSource;
    private long _connectionId;
    private long _openGeneration;
    private TransportState _state = TransportState.Closed;

    /// <summary>UDP 설정으로 전송 인스턴스를 초기화합니다.</summary>
    /// <param name="options">호스트, 포트 등이 포함된 설정입니다.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>가 null일 때.</exception>
    public UdpTransport(UdpTransportOptions options)
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

        _ = CaptureOpenClient();

        await _exclusiveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (long connectionId, UdpClient client) = CaptureOpenClient();
            var session = new UdpTransportSession(this, connectionId, client, _options.ReceiveTimeout, cancellationToken);
            return await work(session).ConfigureAwait(false);
        }
        finally
        {
            _exclusiveLock.Release();
        }
    }
}
