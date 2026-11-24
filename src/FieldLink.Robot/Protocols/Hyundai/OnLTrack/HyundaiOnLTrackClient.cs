using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Shared;
using FieldLink.Transport;
using FieldLink.Transport.Udp;

namespace FieldLink.Robot.Protocols.Hyundai.OnLTrack;

/// <summary>Hyundai Online Tracking UDP 수신 서버 및 6축 증분 명령 송신 클라이언트입니다.</summary>
public sealed partial class HyundaiOnLTrackClient : IDisposable
{
    private readonly IUdpListenerTransport _transport;
    private readonly HyundaiOnLTrackOptions _options;
    private readonly object _endpointLock = new();
    private readonly ProtocolLifecycleGate _lifecycleGate = new();

    private CancellationTokenSource? _receiveLoopCts;
    private Task? _receiveLoopTask;
    private IPEndPoint? _remoteEndPoint;
    private int _sequence = -1;
    private int _isOpen;

    /// <summary>설정 정보로부터 내부 UDP 리스너를 생성해 클라이언트를 초기화합니다.</summary>
    public HyundaiOnLTrackClient(HyundaiOnLTrackOptions? options = null)
        : this(CreateTransport(options ?? new HyundaiOnLTrackOptions()), options)
    {
    }

    /// <summary>사용자 정의 UDP 리스너 전송을 주입받아 클라이언트를 초기화합니다.</summary>
    public HyundaiOnLTrackClient(IUdpListenerTransport transport, HyundaiOnLTrackOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new HyundaiOnLTrackOptions();
    }

    /// <summary>로봇으로부터 유효한 64바이트 트래킹 패킷이 수신될 때 발생합니다.</summary>
    public event Action<HyundaiTrackingPacket>? PacketReceived;

    /// <summary>UDP 수신 서버가 열려 있는지 여부입니다.</summary>
    public bool IsOpen => !_lifecycleGate.IsDisposed && Volatile.Read(ref _isOpen) != 0 && _transport.State == TransportState.Open;

    /// <summary>현재 바인딩된 로컬 UDP 포트 번호입니다.</summary>
    public int BoundPort => _transport.BoundPort;

    /// <summary>최근 패킷을 송신한 로봇 컨트롤러의 원격 엔드포인트입니다.</summary>
    public IPEndPoint? RemoteEndPoint
    {
        get
        {
            lock (_endpointLock)
            {
                return _remoteEndPoint;
            }
        }
    }

    /// <summary>로컬 UDP 포트를 열고 백그라운드 수신 루프를 시작합니다.</summary>
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("Hyundai OnLTrack 서버가 이미 시작되었습니다.");
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                if (!await _lifecycleGate.TryEnterAsync(token).ConfigureAwait(false))
                {
                    throw new ObjectDisposedException(nameof(HyundaiOnLTrackClient));
                }

                try
                {
                    ThrowIfDisposed();
                    if (IsOpen)
                    {
                        throw new InvalidOperationException("Hyundai OnLTrack 서버가 이미 시작되었습니다.");
                    }

                    await _transport.OpenAsync(token).ConfigureAwait(false);
                    if (_lifecycleGate.IsDisposed)
                    {
                        await CloseTransportBestEffortAsync().ConfigureAwait(false);
                        throw new ObjectDisposedException(nameof(HyundaiOnLTrackClient));
                    }

                    var loopCancellation = new CancellationTokenSource();
                    if (_lifecycleGate.IsDisposed)
                    {
                        loopCancellation.Dispose();
                        await CloseTransportBestEffortAsync().ConfigureAwait(false);
                        throw new ObjectDisposedException(nameof(HyundaiOnLTrackClient));
                    }

                    Volatile.Write(ref _isOpen, 1);
                    _receiveLoopCts = loopCancellation;
                    _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(loopCancellation.Token));
                }
                finally
                {
                    _lifecycleGate.Exit();
                }
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>백그라운드 수신 루프를 중지하고 UDP 리스너 소켓을 닫습니다.</summary>
    public Task<RobotResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _isOpen) == 0 || _lifecycleGate.IsDisposed)
        {
            return Task.FromResult(RobotResult.Success());
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                if (!await _lifecycleGate.TryEnterAsync(token).ConfigureAwait(false))
                {
                    return;
                }

                try
                {
                    if (Volatile.Read(ref _isOpen) == 0)
                    {
                        return;
                    }

                    Volatile.Write(ref _isOpen, 0);
                    CancellationTokenSource? loopCancellation = Interlocked.Exchange(ref _receiveLoopCts, null);
                    Task? loopTask = Interlocked.Exchange(ref _receiveLoopTask, null);

                    try
                    {
                        loopCancellation?.Cancel();
                        await _transport.CloseAsync(token).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (loopTask is not null)
                        {
                            try
                            {
                                await loopTask.ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                // 종료 취소에 따른 정상 탈출
                            }
                        }

                        loopCancellation?.Dispose();
                    }
                }
                finally
                {
                    _lifecycleGate.Exit();
                }
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_lifecycleGate.IsDisposed)
        {
            return;
        }

        Volatile.Write(ref _isOpen, 0);
        CancellationTokenSource? loopCancellation = Interlocked.Exchange(ref _receiveLoopCts, null);
        loopCancellation?.Cancel();
        _lifecycleGate.Dispose();
        _transport.Dispose();
        loopCancellation?.Dispose();
    }

    private static UdpListenerTransport CreateTransport(HyundaiOnLTrackOptions options) =>
        new(new UdpListenerTransportOptions(options.LocalPort, options.LocalAddress));

    private bool TryGetSendTarget(
        [NotNullWhen(true)] out IPEndPoint? target,
        [NotNullWhen(false)] out RobotError? error)
    {
        if (!IsOpen)
        {
            target = null;
            error = new RobotError(RobotErrorKind.ConnectionFailed, "Please Start Server First!");
            return false;
        }

        lock (_endpointLock)
        {
            target = _remoteEndPoint;
        }

        if (target is null || IPAddress.Any.Equals(target.Address))
        {
            target = null;
            error = new RobotError(RobotErrorKind.ConnectionFailed, "Please Wait Robot Connect!");
            return false;
        }

        error = null;
        return true;
    }

    private async Task CloseTransportBestEffortAsync()
    {
        try
        {
            await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Dispose와 OpenAsync가 겹칠 때 이미 해제된 전송 계층의 닫기 실패는 무시한다.
        }
    }

    private void ThrowIfDisposed()
    {
        if (_lifecycleGate.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(HyundaiOnLTrackClient));
        }
    }
}
