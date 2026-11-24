using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;
using FieldLink.Transport.Udp;

namespace FieldLink.Robot.Protocols.Hyundai.OnLTrack;

/// <summary>Hyundai Online Tracking UDP 수신 서버 및 6축 증분 명령 송신 클라이언트입니다.</summary>
public sealed class HyundaiOnLTrackClient : IDisposable
{
    private const int ProcessCommandState = 2;

    private readonly IUdpListenerTransport _transport;
    private readonly HyundaiOnLTrackOptions _options;
    private readonly object _endpointLock = new();
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private CancellationTokenSource? _receiveLoopCts;
    private Task? _receiveLoopTask;
    private IPEndPoint? _remoteEndPoint;
    private int _sequence = -1;
    private bool _isOpen;
    private bool _isDisposed;

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
    public bool IsOpen => !_isDisposed && _isOpen && _transport.State == TransportState.Open;

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
                await _lifecycleLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    ThrowIfDisposed();
                    if (IsOpen)
                    {
                        throw new InvalidOperationException("Hyundai OnLTrack 서버가 이미 시작되었습니다.");
                    }

                    await _transport.OpenAsync(token).ConfigureAwait(false);
                    _isOpen = true;
                    _receiveLoopCts = new CancellationTokenSource();
                    _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_receiveLoopCts.Token));
                }
                finally
                {
                    _lifecycleLock.Release();
                }
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>백그라운드 수신 루프를 중지하고 UDP 리스너 소켓을 닫습니다.</summary>
    public async Task<RobotResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        if (!_isOpen)
        {
            return RobotResult.Success();
        }

        _isOpen = false;
        CancellationTokenSource? cts = Interlocked.Exchange(ref _receiveLoopCts, null);
        Task? loopTask = Interlocked.Exchange(ref _receiveLoopTask, null);

        cts?.Cancel();
        RobotResult closeResult = await RobotErrorMapper.ExecuteAsync(
            token => _transport.CloseAsync(token),
            _options.Timeout,
            cancellationToken).ConfigureAwait(false);

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

        cts?.Dispose();
        return closeResult;
    }

    /// <summary>지정된 트래킹 패킷을 학습된 로봇 엔드포인트로 송신합니다.</summary>
    public Task<RobotResult> SendPacketAsync(HyundaiTrackingPacket packet, CancellationToken cancellationToken = default)
    {
        if (packet is null)
        {
            throw new ArgumentNullException(nameof(packet));
        }

        ThrowIfDisposed();
        if (!TryGetSendTarget(out IPEndPoint? target, out RobotError? guardError))
        {
            return Task.FromResult(RobotResult.Failure(guardError!));
        }

        byte[] payload = HyundaiPacketBuilder.BuildPacket(packet);
        return RobotErrorMapper.ExecuteAsync(
            token => _transport.SendToAsync(payload, target!, token),
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>6축 위치 증분 <c>[X(mm), Y(mm), Z(mm), Rx(deg), Ry(deg), Rz(deg)]</c>을 송신합니다.</summary>
    public Task<RobotResult> WriteIncrementPositionAsync(double[] deltaSixAxes, CancellationToken cancellationToken = default)
    {
        if (deltaSixAxes is null)
        {
            throw new ArgumentNullException(nameof(deltaSixAxes));
        }

        if (deltaSixAxes.Length != HyundaiPacketBuilder.AxisCount)
        {
            throw new ArgumentException($"6축 증분 배열의 길이는 정확히 {HyundaiPacketBuilder.AxisCount}이어야 합니다.", nameof(deltaSixAxes));
        }

        for (int i = 0; i < deltaSixAxes.Length; i++)
        {
            if (double.IsNaN(deltaSixAxes[i]) || double.IsInfinity(deltaSixAxes[i]))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSixAxes), deltaSixAxes[i], $"축 인덱스 {i}의 증분 값이 유한한 숫자가 아닙니다.");
            }
        }

        ThrowIfDisposed();
        if (!TryGetSendTarget(out IPEndPoint? target, out RobotError? guardError))
        {
            return Task.FromResult(RobotResult.Failure(guardError!));
        }

        var packet = new HyundaiTrackingPacket
        {
            Command = HyundaiTrackingCommand.Process,
            State = ProcessCommandState,
            Count = NextSequence(),
            Data = deltaSixAxes
        };

        byte[] payload = HyundaiPacketBuilder.BuildPacket(packet);
        return RobotErrorMapper.ExecuteAsync(
            token => _transport.SendToAsync(payload, target!, token),
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>X축 직선 증분 이동(mm)을 송신합니다.</summary>
    public Task<RobotResult> MoveXAsync(double millimeters, CancellationToken cancellationToken = default) =>
        WriteIncrementPositionAsync(new[] { millimeters, 0, 0, 0, 0, 0 }, cancellationToken);

    /// <summary>Y축 직선 증분 이동(mm)을 송신합니다.</summary>
    public Task<RobotResult> MoveYAsync(double millimeters, CancellationToken cancellationToken = default) =>
        WriteIncrementPositionAsync(new[] { 0, millimeters, 0, 0, 0, 0 }, cancellationToken);

    /// <summary>Z축 직선 증분 이동(mm)을 송신합니다.</summary>
    public Task<RobotResult> MoveZAsync(double millimeters, CancellationToken cancellationToken = default) =>
        WriteIncrementPositionAsync(new[] { 0, 0, millimeters, 0, 0, 0 }, cancellationToken);

    /// <summary>Rx축 회전 증분(deg)을 송신합니다.</summary>
    public Task<RobotResult> RotateXAsync(double degrees, CancellationToken cancellationToken = default) =>
        WriteIncrementPositionAsync(new[] { 0, 0, 0, degrees, 0, 0 }, cancellationToken);

    /// <summary>Ry축 회전 증분(deg)을 송신합니다.</summary>
    public Task<RobotResult> RotateYAsync(double degrees, CancellationToken cancellationToken = default) =>
        WriteIncrementPositionAsync(new[] { 0, 0, 0, 0, degrees, 0 }, cancellationToken);

    /// <summary>Rz축 회전 증분(deg)을 송신합니다.</summary>
    public Task<RobotResult> RotateZAsync(double degrees, CancellationToken cancellationToken = default) =>
        WriteIncrementPositionAsync(new[] { 0, 0, 0, 0, 0, degrees }, cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isOpen = false;
        CancellationTokenSource? cts = Interlocked.Exchange(ref _receiveLoopCts, null);
        cts?.Cancel();
        cts?.Dispose();
        _transport.Dispose();
    }

    private static UdpListenerTransport CreateTransport(HyundaiOnLTrackOptions options) =>
        new(new UdpListenerTransportOptions(options.LocalPort, options.LocalAddress));

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsOpen)
        {
            try
            {
                UdpDatagram datagram = await _transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                await HandleIncomingDatagramAsync(datagram, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (TransportException)
            {
                if (cancellationToken.IsCancellationRequested || !IsOpen)
                {
                    break;
                }
            }
            catch (RobotException)
            {
                // 손상된 패킷 명령 바이트는 무시하고 다음 데이터그램을 계속 대기
            }
        }
    }

    private async Task HandleIncomingDatagramAsync(UdpDatagram datagram, CancellationToken cancellationToken)
    {
        byte[] rawPayload = datagram.GetPayload();
        if (rawPayload.Length != HyundaiPacketBuilder.PacketLength)
        {
            return;
        }

        HyundaiTrackingPacket packet = HyundaiPacketParser.ParsePacket(rawPayload);
        lock (_endpointLock)
        {
            _remoteEndPoint = datagram.RemoteEndPoint;
        }

        if (packet.Command == HyundaiTrackingCommand.Start)
        {
            byte[] ack = HyundaiPacketBuilder.BuildStartAck(rawPayload);
            await _transport.SendToAsync(ack, datagram.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            PacketReceived?.Invoke(packet);
        }
        catch (Exception)
        {
            // 이벤트 구독자 예외가 백그라운드 수신 루프를 중단시키지 않도록 격리한다.
        }
    }

    private bool TryGetSendTarget(out IPEndPoint? target, out RobotError? error)
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
            error = new RobotError(RobotErrorKind.ConnectionFailed, "Please Wait Robot Connect!");
            return false;
        }

        error = null;
        return true;
    }

    private int NextSequence()
    {
        lock (_endpointLock)
        {
            _sequence = _sequence == int.MaxValue ? 0 : _sequence + 1;
            return _sequence;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(HyundaiOnLTrackClient));
        }
    }
}
