using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC Robot Interface (SRTP/SNPX over TCP) 통신 클라이언트입니다.</summary>
public sealed class FanucClient : IRobotClient
{
    private const ushort SnapshotWordCount = 6130;
    private const ushort StringRegisterReadWordCount = 40;
    private const int PoseXyzwprCount = 9;
    private const int PoseConfigCount = 7;
    private const ushort JointPoseWordOffset = 26;
    private const ushort PoseValidFlagWordOffset = 44;
    private const ushort UserFrameWordOffset = 45;
    private const ushort UserToolWordOffset = 46;
    private const short MinimumFrameNumber = 0;
    private const short MaximumFrameNumber = 15;
    private const int BytesPerWord = 2;
    private const string SnapshotPseudoAddress = "snapshot";

    private readonly ITransport _transport;
    private readonly FanucOptions _options;
    private readonly FanucFrameDecoder _frameDecoder = new();
    private readonly object _cacheLock = new();

    private long _initializedConnectionId = -1;
    private FanucSnapshot? _cachedSnapshot;
    private DateTime _cacheTimestampUtc;
    private bool _isOpen;
    private bool _isDisposed;

    /// <summary>전송 구현체와 옵션으로 FANUC 클라이언트를 초기화합니다.</summary>
    public FanucClient(ITransport transport, FanucOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new FanucOptions();
    }

    /// <inheritdoc />
    public bool IsOpen => !_isDisposed && _isOpen && _transport.State == TransportState.Open;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("FANUC 클라이언트가 이미 열려 있습니다.");
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                await _transport.OpenAsync(token).ConfigureAwait(false);
                _isOpen = true;
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        if (!_isOpen)
        {
            return Task.FromResult(RobotResult.Success());
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                _isOpen = false;
                _initializedConnectionId = -1;
                ClearCachedSnapshot();
                await _transport.CloseAsync(token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>셀렉터와 1-based 워드 주소로 원시 워드 바이트를 읽습니다.</summary>
    public Task<RobotResult<byte[]>> ReadWordsAsync(
        FanucSelector selector,
        ushort address1Based,
        ushort wordCount,
        CancellationToken cancellationToken = default)
    {
        ValidateAddressAndCount(address1Based, wordCount, nameof(wordCount), "읽을 워드 수는 1 이상이어야 합니다.");

        ThrowIfDisposed();
        EnsureOpen();
        if (!IsWordSelector(selector))
        {
            return Task.FromResult(RobotResult.Failure<byte[]>(
                new RobotError(RobotErrorKind.Unsupported, $"셀렉터 '{selector}'는 워드 연산을 지원하지 않습니다.")));
        }

        return RobotErrorMapper.ExecuteAsync(
            token => _transport.RunExclusiveAsync(
                session => ExchangeReadAsync(session, selector, address1Based, wordCount, wordCount * 2),
                token),
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>셀렉터와 1-based 워드 주소에 원시 워드 바이트를 씁니다.</summary>
    public Task<RobotResult> WriteWordsAsync(
        FanucSelector selector,
        ushort address1Based,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (address1Based == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address1Based), address1Based, "FANUC 주소는 1 이상이어야 합니다.");
        }

        if (data.Length == 0 || data.Length % BytesPerWord != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(data), data.Length, "워드 쓰기 바이트 길이는 0보다 큰 짝수여야 합니다.");
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!IsWordSelector(selector))
        {
            return Task.FromResult(RobotResult.Failure(
                new RobotError(RobotErrorKind.Unsupported, $"셀렉터 '{selector}'는 워드 연산을 지원하지 않습니다.")));
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                await _transport.RunExclusiveAsync(
                    async session =>
                    {
                        await EnsureHandshakeAsync(session).ConfigureAwait(false);
                        await ExchangeWriteAsync(session, selector, address1Based, data, (ushort)(data.Length / 2)).ConfigureAwait(false);
                        return true;
                    },
                    token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>셀렉터와 1-based 비트 주소로 비트 배열을 읽습니다.</summary>
    public Task<RobotResult<bool[]>> ReadBitsAsync(
        FanucSelector selector,
        ushort address1Based,
        ushort bitCount,
        CancellationToken cancellationToken = default)
    {
        ValidateAddressAndCount(address1Based, bitCount, nameof(bitCount), "읽을 비트 수는 1 이상이어야 합니다.");

        ThrowIfDisposed();
        EnsureOpen();
        if (!IsBitSelector(selector))
        {
            return Task.FromResult(RobotResult.Failure<bool[]>(
                new RobotError(RobotErrorKind.Unsupported, $"셀렉터 '{selector}'는 비트 연산을 지원하지 않습니다.")));
        }

        return RobotErrorMapper.ExecuteAsync(
            token => _transport.RunExclusiveAsync(
                async session =>
                {
                    (int bitOffset, int byteLength) = FanucRequestBuilder.CalculateBitAlignment(address1Based, bitCount);
                    byte[] bytes = await ExchangeReadAsync(session, selector, address1Based, (ushort)(byteLength * 8), byteLength).ConfigureAwait(false);
                    bool[] allBits = RobotBitPacking.Unpack(bytes, 0, byteLength * 8);
                    bool[] result = new bool[bitCount];
                    Array.Copy(allBits, bitOffset, result, 0, bitCount);
                    return result;
                },
                token),
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>셀렉터와 1-based 비트 주소에 비트 배열을 씁니다.</summary>
    public Task<RobotResult> WriteBitsAsync(
        FanucSelector selector,
        ushort address1Based,
        bool[] values,
        CancellationToken cancellationToken = default)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (address1Based == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address1Based), address1Based, "FANUC 주소는 1 이상이어야 합니다.");
        }

        if (values.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(values), values.Length, "쓸 비트 수는 1 이상이어야 합니다.");
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!IsBitSelector(selector))
        {
            return Task.FromResult(RobotResult.Failure(
                new RobotError(RobotErrorKind.Unsupported, $"셀렉터 '{selector}'는 비트 연산을 지원하지 않습니다.")));
        }

        byte[] payload = FanucRequestBuilder.BuildBitWritePayload(address1Based, values);

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                await _transport.RunExclusiveAsync(
                    async session =>
                    {
                        await EnsureHandshakeAsync(session).ConfigureAwait(false);
                        await ExchangeWriteAsync(session, selector, address1Based, payload, (ushort)values.Length).ConfigureAwait(false);
                        return true;
                    },
                    token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>문자열 주소(<c>D</c>, <c>R</c>, <c>SR</c>, <c>AI</c>, <c>AQ</c>, <c>GI</c>, <c>GO</c>)로 워드를 읽습니다.</summary>
    public Task<RobotResult<byte[]>> ReadAddressWordsAsync(string address, ushort wordCount, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!FanucAddress.TryParse(address, isBit: false, out FanucAddress? parsed))
        {
            return Task.FromResult(RobotResult.Failure<byte[]>(
                new RobotError(RobotErrorKind.ParseError, $"유효하지 않은 FANUC 워드 주소입니다: '{address}'.")));
        }

        return ReadWordsAsync(parsed.Selector, parsed.Address, wordCount, cancellationToken);
    }

    /// <summary>문자열 주소에 워드 바이트를 씁니다.</summary>
    public Task<RobotResult> WriteAddressWordsAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!FanucAddress.TryParse(address, isBit: false, out FanucAddress? parsed))
        {
            return Task.FromResult(RobotResult.Failure(
                new RobotError(RobotErrorKind.ParseError, $"유효하지 않은 FANUC 워드 주소입니다: '{address}'.")));
        }

        return WriteWordsAsync(parsed.Selector, parsed.Address, data, cancellationToken);
    }

    /// <summary>문자열 비트 주소(<c>SDO</c>, <c>SDI</c>, <c>RDO</c>, <c>RDI</c>, <c>I</c>, <c>Q</c>, <c>M</c> 등)로 비트를 읽습니다.</summary>
    public Task<RobotResult<bool[]>> ReadAddressBitsAsync(string address, ushort bitCount, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!FanucAddress.TryParse(address, isBit: true, out FanucAddress? parsed))
        {
            return Task.FromResult(RobotResult.Failure<bool[]>(
                new RobotError(RobotErrorKind.ParseError, $"유효하지 않은 FANUC 비트 주소입니다: '{address}'.")));
        }

        return ReadBitsAsync(parsed.Selector, parsed.Address, bitCount, cancellationToken);
    }

    /// <summary>문자열 비트 주소에 비트 배열을 씁니다.</summary>
    public Task<RobotResult> WriteAddressBitsAsync(string address, bool[] values, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!FanucAddress.TryParse(address, isBit: true, out FanucAddress? parsed))
        {
            return Task.FromResult(RobotResult.Failure(
                new RobotError(RobotErrorKind.ParseError, $"유효하지 않은 FANUC 비트 주소입니다: '{address}'.")));
        }

        return WriteBitsAsync(parsed.Selector, parsed.Address, values, cancellationToken);
    }

    /// <summary>전체 FANUC 로봇 스냅샷(<c>D1</c> 기준 6130워드)을 조회합니다.</summary>
    public Task<RobotResult<FanucSnapshot>> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();

        if (TryGetCachedSnapshot(out FanucSnapshot? cached))
        {
            return Task.FromResult(RobotResult.Success(cached!));
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                byte[] rawData = await _transport.RunExclusiveAsync(
                    session => ExchangeReadAsync(session, FanucSelector.Data, 1, SnapshotWordCount, SnapshotWordCount * 2),
                    token).ConfigureAwait(false);
                FanucSnapshot snapshot = FanucResponseParser.ParseSnapshot(rawData, _options.StringEncoding);
                CacheSnapshot(snapshot);
                return snapshot;
            },
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
            throw new ArgumentException("직교 자세는 Xyzwpr 9개와 Config 7개 요소를 포함해야 합니다.", nameof(pose));
        }

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
                        await ExchangeWriteAsync(session, FanucSelector.Data, wordAddress, posePayload, (ushort)(posePayload.Length / 2)).ConfigureAwait(false);
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
            throw new ArgumentException("조인트 자세는 9개 축 요소를 포함해야 합니다.", nameof(pose));
        }

        if (wordAddress == 0 || wordAddress > ushort.MaxValue - UserToolWordOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(wordAddress), wordAddress, "직교 자세를 기록할 FANUC 워드 주소가 유효 범위를 벗어났습니다.");
        }

        if (wordAddress == 0 || wordAddress > ushort.MaxValue - UserToolWordOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(wordAddress), wordAddress, "조인트 자세를 기록할 FANUC 워드 주소가 유효 범위를 벗어났습니다.");
        }

        ThrowIfDisposed();
        EnsureOpen();

        byte[] jointBytes = BuildSingleArrayPayload(pose.Joint);
        byte[] validFlagBytes = new byte[2];

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                await _transport.RunExclusiveAsync(
                    async session =>
                    {
                        await EnsureHandshakeAsync(session).ConfigureAwait(false);
                        await ExchangeWriteAsync(session, FanucSelector.Data, checked((ushort)(wordAddress + JointPoseWordOffset)), jointBytes, (ushort)(jointBytes.Length / 2)).ConfigureAwait(false);
                        await ExchangeWriteAsync(session, FanucSelector.Data, checked((ushort)(wordAddress + PoseValidFlagWordOffset)), validFlagBytes, 1).ConfigureAwait(false);
                        await WriteFrameNumbersAsync(session, wordAddress, pose.UserFrame, pose.UserTool).ConfigureAwait(false);
                        return true;
                    },
                    token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default) =>
        ReadAddressWordsAsync(address, 1, cancellationToken);

    /// <inheritdoc />
    public async Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (string.IsNullOrWhiteSpace(address) || string.Equals(address, SnapshotPseudoAddress, StringComparison.OrdinalIgnoreCase))
        {
            RobotResult<FanucSnapshot> snapshot = await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            return snapshot.IsSuccess
                ? RobotResult.Success(snapshot.Value.ToJson())
                : RobotResult.Failure<string>(snapshot.Error);
        }

        RobotResult<byte[]> words = await ReadAddressWordsAsync(address, StringRegisterReadWordCount, cancellationToken).ConfigureAwait(false);
        return words.IsSuccess
            ? RobotResult.Success(RobotBinaryBuffer.ReadNullTrimmedString(words.Value, 0, words.Value.Length, _options.StringEncoding))
            : RobotResult.Failure<string>(words.Error);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default) =>
        WriteAddressWordsAsync(address, data, cancellationToken);

    /// <inheritdoc />
    public Task<RobotResult> WriteStringAsync(string address, string value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        byte[] encoded = _options.StringEncoding.GetBytes(value);
        if (encoded.Length == 0)
        {
            encoded = new byte[BytesPerWord];
        }
        else if (encoded.Length % BytesPerWord != 0)
        {
            Array.Resize(ref encoded, encoded.Length + 1);
        }

        return WriteAddressWordsAsync(address, encoded, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isOpen = false;
        _transport.Dispose();
    }

    private async Task<byte[]> ExchangeReadAsync(
        ITransportSession session,
        FanucSelector selector,
        ushort address1Based,
        ushort wireLength,
        int expectedByteCount)
    {
        await EnsureHandshakeAsync(session).ConfigureAwait(false);
        byte[] request = FanucRequestBuilder.BuildReadRequest(selector, address1Based, wireLength);
        await session.SendAsync(request).ConfigureAwait(false);
        byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
        return FanucResponseParser.ParseReadResponse(response, expectedByteCount);
    }

    private async Task ExchangeWriteAsync(
        ITransportSession session,
        FanucSelector selector,
        ushort address1Based,
        byte[] value,
        ushort wireLength)
    {
        byte[] request = FanucRequestBuilder.BuildWriteRequest(selector, address1Based, value, wireLength);
        await session.SendAsync(request).ConfigureAwait(false);
        byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
        FanucResponseParser.ParseWriteResponse(response);
    }

    private async Task WriteFrameNumbersAsync(ITransportSession session, ushort wordAddress, short userFrame, short userTool)
    {
        bool validUf = userFrame is >= MinimumFrameNumber and <= MaximumFrameNumber;
        bool validUt = userTool is >= MinimumFrameNumber and <= MaximumFrameNumber;

        if (validUf && validUt)
        {
            byte[] both = new byte[4];
            RobotBinaryBuffer.WriteInt16LittleEndian(both, 0, userFrame);
            RobotBinaryBuffer.WriteInt16LittleEndian(both, 2, userTool);
            await ExchangeWriteAsync(session, FanucSelector.Data, checked((ushort)(wordAddress + UserFrameWordOffset)), both, 2).ConfigureAwait(false);
            return;
        }

        if (validUf)
        {
            byte[] ufBytes = new byte[2];
            RobotBinaryBuffer.WriteInt16LittleEndian(ufBytes, 0, userFrame);
            await ExchangeWriteAsync(session, FanucSelector.Data, checked((ushort)(wordAddress + UserFrameWordOffset)), ufBytes, 1).ConfigureAwait(false);
        }
        else if (validUt)
        {
            byte[] utBytes = new byte[2];
            RobotBinaryBuffer.WriteInt16LittleEndian(utBytes, 0, userTool);
            await ExchangeWriteAsync(session, FanucSelector.Data, checked((ushort)(wordAddress + UserToolWordOffset)), utBytes, 1).ConfigureAwait(false);
        }
    }

    private async Task EnsureHandshakeAsync(ITransportSession session)
    {
        if (_initializedConnectionId == session.ConnectionId)
        {
            return;
        }

        await session.SendAsync(FanucRequestBuilder.BuildConnectRequest(_options.ClientId)).ConfigureAwait(false);
        await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);

        await session.SendAsync(FanucRequestBuilder.BuildSessionInitRequest()).ConfigureAwait(false);
        await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);

        foreach (string command in FanucSnpxAssignments.Commands)
        {
            byte[] commandBytes = Encoding.ASCII.GetBytes(command);
            await ExchangeWriteAsync(session, FanucSelector.SnpxCommand, 1, commandBytes, (ushort)commandBytes.Length).ConfigureAwait(false);
        }

        _initializedConnectionId = session.ConnectionId;
    }

    private static byte[] BuildCartesianPayload(IReadOnlyList<float> xyzwpr, IReadOnlyList<short> config)
    {
        int byteCount = (xyzwpr.Count * 4) + (config.Count * 2) + 2;
        byte[] payload = new byte[byteCount];
        for (int i = 0; i < xyzwpr.Count; i++)
        {
            RobotBinaryBuffer.WriteSingleLittleEndian(payload, i * 4, xyzwpr[i]);
        }

        int configOffset = xyzwpr.Count * 4;
        for (int i = 0; i < config.Count; i++)
        {
            RobotBinaryBuffer.WriteInt16LittleEndian(payload, configOffset + (i * 2), config[i]);
        }

        return payload;
    }

    private static byte[] BuildSingleArrayPayload(IReadOnlyList<float> values)
    {
        byte[] payload = new byte[values.Count * 4];
        for (int i = 0; i < values.Count; i++)
        {
            RobotBinaryBuffer.WriteSingleLittleEndian(payload, i * 4, values[i]);
        }

        return payload;
    }

    private bool TryGetCachedSnapshot(out FanucSnapshot? snapshot)
    {
        lock (_cacheLock)
        {
            if (_cachedSnapshot is not null
                && _options.SnapshotCacheDuration > TimeSpan.Zero
                && (DateTime.UtcNow - _cacheTimestampUtc) <= _options.SnapshotCacheDuration)
            {
                snapshot = _cachedSnapshot;
                return true;
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
            _cacheTimestampUtc = DateTime.UtcNow;
        }
    }

    private void ClearCachedSnapshot()
    {
        lock (_cacheLock)
        {
            _cachedSnapshot = null;
            _cacheTimestampUtc = default;
        }
    }

    private static void ValidateAddressAndCount(ushort address1Based, ushort count, string countParameterName, string countErrorMessage)
    {
        if (address1Based == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address1Based), address1Based, "FANUC 주소는 1 이상이어야 합니다.");
        }

        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(countParameterName, count, countErrorMessage);
        }
    }

    private static bool IsBitSelector(FanucSelector selector) =>
        selector is FanucSelector.InputRelay or FanucSelector.OutputRelay or FanucSelector.InternalRelay;

    private static bool IsWordSelector(FanucSelector selector) =>
        selector is FanucSelector.Data or FanucSelector.AnalogInput or FanucSelector.AnalogOutput;

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("FANUC 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(FanucClient));
        }
    }
}
