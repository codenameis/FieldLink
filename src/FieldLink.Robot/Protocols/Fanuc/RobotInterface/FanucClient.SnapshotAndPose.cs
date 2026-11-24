using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

public sealed partial class FanucClient
{
    private const ushort SnapshotWordCount = 6130;
    private const int PoseXyzwprCount = 9;
    private const int PoseConfigCount = 7;
    private const ushort JointPoseWordOffset = 26;
    private const ushort PoseValidFlagWordOffset = 44;
    private const ushort UserFrameWordOffset = 45;
    private const ushort UserToolWordOffset = 46;
    private const short MinimumFrameNumber = 0;
    private const short MaximumFrameNumber = 15;
    private const int SingleByteSize = sizeof(float);

    /// <summary>전체 FANUC 로봇 스냅샷(<c>D1</c> 기준 6130워드)을 조회합니다.</summary>
    public Task<RobotResult<FanucSnapshot>> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();

        if (TryGetCachedSnapshot(out FanucSnapshot? cached))
        {
            return Task.FromResult(RobotResult.Success(cached));
        }

        return RobotErrorMapper.ExecuteAsync(
            ReadSnapshotCoreAsync,
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>지정 <c>D</c> 워드 주소에 직교 좌표 자세(<c>Xyzwpr</c>, <c>Config</c>, <c>UF</c>, <c>UT</c>)를 기록합니다.</summary>
    public Task<RobotResult> WriteCartesianPoseAsync(
        ushort wordAddress,
        FanucPose pose,
        CancellationToken cancellationToken = default)
    {
        if (pose is null)
        {
            throw new ArgumentNullException(nameof(pose));
        }

        if (pose.Xyzwpr.Count != PoseXyzwprCount || pose.Config.Count != PoseConfigCount)
        {
            throw new ArgumentException($"직교 자세는 Xyzwpr {PoseXyzwprCount}개와 Config {PoseConfigCount}개 요소를 포함해야 합니다.", nameof(pose));
        }

        ValidateFiniteSingles(pose.Xyzwpr, nameof(pose));
        if (wordAddress == 0 || wordAddress > ushort.MaxValue - UserToolWordOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(wordAddress), wordAddress, "직교 자세를 기록할 FANUC 워드 주소가 유효 범위를 벗어났습니다.");
        }

        ThrowIfDisposed();
        EnsureOpen();

        byte[] posePayload = BuildCartesianPayload(pose.Xyzwpr, pose.Config);
        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                await _transport.RunExclusiveAsync(
                    async session =>
                    {
                        await EnsureHandshakeAsync(session).ConfigureAwait(false);
                        await ExchangeWriteAsync(session, FanucSelector.Data, wordAddress, posePayload, (ushort)(posePayload.Length / BytesPerWord)).ConfigureAwait(false);
                        await WriteFrameNumbersAsync(session, wordAddress, pose.UserFrame, pose.UserTool).ConfigureAwait(false);
                        return true;
                    },
                    token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>지정 <c>D</c> 워드 주소에 조인트 좌표 자세(<c>Joint</c>, <c>UF</c>, <c>UT</c>)를 기록합니다. (사양서 §9.1 UT 오프셋 수정 반영)</summary>
    public Task<RobotResult> WriteJointPoseAsync(
        ushort wordAddress,
        FanucPose pose,
        CancellationToken cancellationToken = default)
    {
        if (pose is null)
        {
            throw new ArgumentNullException(nameof(pose));
        }

        if (pose.Joint.Count != PoseXyzwprCount)
        {
            throw new ArgumentException($"조인트 자세는 {PoseXyzwprCount}개 축 요소를 포함해야 합니다.", nameof(pose));
        }

        ValidateFiniteSingles(pose.Joint, nameof(pose));
        if (wordAddress == 0 || wordAddress > ushort.MaxValue - UserToolWordOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(wordAddress), wordAddress, "조인트 자세를 기록할 FANUC 워드 주소가 유효 범위를 벗어났습니다.");
        }

        ThrowIfDisposed();
        EnsureOpen();

        byte[] jointBytes = BuildSingleArrayPayload(pose.Joint);
        byte[] validFlagBytes = new byte[BytesPerWord];

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                await _transport.RunExclusiveAsync(
                    async session =>
                    {
                        await EnsureHandshakeAsync(session).ConfigureAwait(false);
                        await ExchangeWriteAsync(session, FanucSelector.Data, checked((ushort)(wordAddress + JointPoseWordOffset)), jointBytes, (ushort)(jointBytes.Length / BytesPerWord)).ConfigureAwait(false);
                        await ExchangeWriteAsync(session, FanucSelector.Data, checked((ushort)(wordAddress + PoseValidFlagWordOffset)), validFlagBytes, 1).ConfigureAwait(false);
                        await WriteFrameNumbersAsync(session, wordAddress, pose.UserFrame, pose.UserTool).ConfigureAwait(false);
                        return true;
                    },
                    token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    private async Task<FanucSnapshot> ReadSnapshotCoreAsync(CancellationToken cancellationToken)
    {
        if (TryGetCachedSnapshot(out FanucSnapshot? cached))
        {
            return cached;
        }

        byte[] rawData = await _transport.RunExclusiveAsync(
            session => ExchangeReadAsync(session, FanucSelector.Data, 1, SnapshotWordCount, SnapshotWordCount * BytesPerWord),
            cancellationToken).ConfigureAwait(false);
        FanucSnapshot snapshot = FanucResponseParser.ParseSnapshot(rawData, _options.StringEncoding);
        CacheSnapshot(snapshot);
        return snapshot;
    }

    private async Task WriteFrameNumbersAsync(ITransportSession session, ushort wordAddress, short userFrame, short userTool)
    {
        bool validUf = userFrame is >= MinimumFrameNumber and <= MaximumFrameNumber;
        bool validUt = userTool is >= MinimumFrameNumber and <= MaximumFrameNumber;

        if (validUf && validUt)
        {
            byte[] both = new byte[BytesPerWord * 2];
            RobotBinaryBuffer.WriteInt16LittleEndian(both, 0, userFrame);
            RobotBinaryBuffer.WriteInt16LittleEndian(both, BytesPerWord, userTool);
            await ExchangeWriteAsync(session, FanucSelector.Data, checked((ushort)(wordAddress + UserFrameWordOffset)), both, 2).ConfigureAwait(false);
            return;
        }

        if (validUf)
        {
            byte[] userFrameBytes = new byte[BytesPerWord];
            RobotBinaryBuffer.WriteInt16LittleEndian(userFrameBytes, 0, userFrame);
            await ExchangeWriteAsync(session, FanucSelector.Data, checked((ushort)(wordAddress + UserFrameWordOffset)), userFrameBytes, 1).ConfigureAwait(false);
        }
        else if (validUt)
        {
            byte[] userToolBytes = new byte[BytesPerWord];
            RobotBinaryBuffer.WriteInt16LittleEndian(userToolBytes, 0, userTool);
            await ExchangeWriteAsync(session, FanucSelector.Data, checked((ushort)(wordAddress + UserToolWordOffset)), userToolBytes, 1).ConfigureAwait(false);
        }
    }

    private static void ValidateFiniteSingles(IReadOnlyList<float> values, string parameterName)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
            {
                throw new ArgumentOutOfRangeException(parameterName, values[i], $"축 인덱스 {i}의 좌표 값이 유한한 숫자가 아닙니다.");
            }
        }
    }

    private static byte[] BuildCartesianPayload(IReadOnlyList<float> xyzwpr, IReadOnlyList<short> config)
    {
        int byteCount = (xyzwpr.Count * SingleByteSize) + (config.Count * BytesPerWord) + BytesPerWord;
        byte[] payload = new byte[byteCount];
        for (int i = 0; i < xyzwpr.Count; i++)
        {
            RobotBinaryBuffer.WriteSingleLittleEndian(payload, i * SingleByteSize, xyzwpr[i]);
        }

        int configOffset = xyzwpr.Count * SingleByteSize;
        for (int i = 0; i < config.Count; i++)
        {
            RobotBinaryBuffer.WriteInt16LittleEndian(payload, configOffset + (i * BytesPerWord), config[i]);
        }

        return payload;
    }

    private static byte[] BuildSingleArrayPayload(IReadOnlyList<float> values)
    {
        byte[] payload = new byte[values.Count * SingleByteSize];
        for (int i = 0; i < values.Count; i++)
        {
            RobotBinaryBuffer.WriteSingleLittleEndian(payload, i * SingleByteSize, values[i]);
        }

        return payload;
    }

    private bool TryGetCachedSnapshot([NotNullWhen(true)] out FanucSnapshot? snapshot)
    {
        lock (_cacheLock)
        {
            if (_cachedSnapshot is not null && _options.SnapshotCacheDuration > TimeSpan.Zero)
            {
                long elapsedTicks = _timestampProvider() - _cacheTimestampTicks;
                if (elapsedTicks >= 0 && ((double)elapsedTicks / Stopwatch.Frequency) <= _options.SnapshotCacheDuration.TotalSeconds)
                {
                    snapshot = _cachedSnapshot;
                    return true;
                }
            }

            snapshot = null;
            return false;
        }
    }

    private void CacheSnapshot(FanucSnapshot snapshot)
    {
        lock (_cacheLock)
        {
            _cachedSnapshot = snapshot;
            _cacheTimestampTicks = _timestampProvider();
        }
    }

    private void ClearCachedSnapshot()
    {
        lock (_cacheLock)
        {
            _cachedSnapshot = null;
            _cacheTimestampTicks = 0;
        }
    }
}
