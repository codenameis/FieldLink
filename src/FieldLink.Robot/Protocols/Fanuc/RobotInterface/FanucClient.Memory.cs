using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

public sealed partial class FanucClient
{
    private const ushort MaximumWordCountPerRequest = (ushort)short.MaxValue;
    private const int MaximumWriteWordByteCount = ushort.MaxValue - 1;
    private const int BitsPerByte = 8;

    /// <summary>셀렉터와 1-based 워드 주소로 원시 워드 바이트를 읽습니다.</summary>
    public Task<RobotResult<byte[]>> ReadWordsAsync(
        FanucSelector selector,
        ushort address1Based,
        ushort wordCount,
        CancellationToken cancellationToken = default)
    {
        ValidateAddressAndCount(address1Based, wordCount, nameof(wordCount), "읽을 워드 수는 1 이상이어야 합니다.");
        if (wordCount > MaximumWordCountPerRequest)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordCount),
                wordCount,
                $"읽을 워드 수는 최대 {MaximumWordCountPerRequest}이어야 합니다.");
        }

        ThrowIfDisposed();
        EnsureOpen();
        if (!IsWordSelector(selector))
        {
            return Task.FromResult(RobotResult.Failure<byte[]>(
                new RobotError(RobotErrorKind.Unsupported, $"셀렉터 '{selector}'는 워드 연산을 지원하지 않습니다.")));
        }

        return RobotErrorMapper.ExecuteAsync(
            token => _transport.RunExclusiveAsync(
                session => ExchangeReadAsync(session, selector, address1Based, wordCount, wordCount * BytesPerWord),
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

        if (data.Length == 0 || data.Length % BytesPerWord != 0 || data.Length > MaximumWriteWordByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                data.Length,
                $"워드 쓰기 바이트 길이는 2에서 {MaximumWriteWordByteCount} 사이의 짝수여야 합니다.");
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
                        await ExchangeWriteAsync(session, selector, address1Based, data, (ushort)(data.Length / BytesPerWord)).ConfigureAwait(false);
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
        (int bitOffset, int byteLength) = FanucRequestBuilder.CalculateBitAlignment(address1Based, bitCount);
        if (byteLength * BitsPerByte > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitCount),
                bitCount,
                "바이트 정렬된 비트 읽기 길이가 16비트 SRTP 한도(65535비트)를 초과합니다.");
        }

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
                    byte[] bytes = await ExchangeReadAsync(session, selector, address1Based, (ushort)(byteLength * BitsPerByte), byteLength).ConfigureAwait(false);
                    bool[] allBits = RobotBitPacking.Unpack(bytes, 0, byteLength * BitsPerByte);
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

        if (values.Length == 0 || values.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                values.Length,
                $"쓸 비트 수는 1에서 {ushort.MaxValue} 사이여야 합니다.");
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
        ClearCachedSnapshot();
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
}
