using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Hyundai.OnLTrack;

public sealed partial class HyundaiOnLTrackClient
{
    private const int ProcessCommandState = 2;

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
            return Task.FromResult(RobotResult.Failure(guardError));
        }

        byte[] payload = HyundaiPacketBuilder.BuildPacket(packet);
        return RobotErrorMapper.ExecuteAsync(
            token => _transport.SendToAsync(payload, target, token),
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
            return Task.FromResult(RobotResult.Failure(guardError));
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
            token => _transport.SendToAsync(payload, target, token),
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

    private int NextSequence()
    {
        lock (_endpointLock)
        {
            _sequence = _sequence == int.MaxValue ? 0 : _sequence + 1;
            return _sequence;
        }
    }
}
