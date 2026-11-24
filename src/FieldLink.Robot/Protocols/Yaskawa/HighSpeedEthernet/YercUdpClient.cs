using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA High-Speed Ethernet (<c>YERC</c>) UDP 바이너리 프로토콜 클라이언트입니다.</summary>
public sealed partial class YercUdpClient : IRobotClient
{
    private const byte SinglePointAttribute = 1;
    private const int MinimumAddressLength = 2;

    private readonly ITransport _transport;
    private readonly YercUdpOptions _options;
    private readonly YercFrameDecoder _frameDecoder = new();
    private readonly ProtocolLifecycleGate _lifecycleGate = new();
    private readonly object _requestIdLock = new();

    private byte _requestId;
    private int _isOpen;

    /// <summary>전송 구현체와 옵션으로 High-Speed Ethernet 클라이언트를 초기화합니다.</summary>
    public YercUdpClient(ITransport transport, YercUdpOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new YercUdpOptions();
    }

    /// <inheritdoc />
    public bool IsOpen => !_lifecycleGate.IsDisposed && Volatile.Read(ref _isOpen) != 0 && _transport.State == TransportState.Open;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("YASKAWA High-Speed UDP 클라이언트가 이미 열려 있습니다.");
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                if (!await _lifecycleGate.TryEnterAsync(token).ConfigureAwait(false))
                {
                    throw new ObjectDisposedException(nameof(YercUdpClient));
                }

                try
                {
                    ThrowIfDisposed();
                    if (IsOpen)
                    {
                        throw new InvalidOperationException("YASKAWA High-Speed UDP 클라이언트가 이미 열려 있습니다.");
                    }

                    await _transport.OpenAsync(token).ConfigureAwait(false);
                    if (_lifecycleGate.IsDisposed)
                    {
                        await CloseTransportBestEffortAsync().ConfigureAwait(false);
                        throw new ObjectDisposedException(nameof(YercUdpClient));
                    }

                    Volatile.Write(ref _isOpen, 1);
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
                    await _transport.CloseAsync(token).ConfigureAwait(false);
                }
                finally
                {
                    _lifecycleGate.Exit();
                }
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>임의 <c>YERC</c> 명령을 전송하고 응답 페이로드 바이트를 반환합니다.</summary>
    public Task<RobotResult<byte[]>> SendCommandAsync(
        YercCommandHeader header,
        byte[]? dataPart = null,
        CancellationToken cancellationToken = default)
    {
        if (header is null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            token => ExchangePayloadAsync(header, dataPart, token),
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default) =>
        SendCommandAsync(ParseAddressHeader(address, isWrite: false), null, cancellationToken);

    /// <inheritdoc />
    public async Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        RobotResult<byte[]> raw = await ReadRawAsync(address, cancellationToken).ConfigureAwait(false);
        return raw.IsSuccess
            ? RobotResult.Success(BitConverter.ToString(raw.Value))
            : RobotResult.Failure<string>(raw.Error);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return SendControlAsync(ParseAddressHeader(address, isWrite: true), data, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteStringAsync(string address, string value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return WriteRawAsync(address, _options.StringEncoding.GetBytes(value), cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_lifecycleGate.IsDisposed)
        {
            return;
        }

        Volatile.Write(ref _isOpen, 0);
        _lifecycleGate.Dispose();
        _transport.Dispose();
    }

    private Task<RobotResult<T>> SendAndParseAsync<T>(YercCommandHeader header, Func<byte[], T> parser, CancellationToken cancellationToken) =>
        SendAndParseAsync(header, null, parser, cancellationToken);

    private Task<RobotResult<T>> SendAndParseAsync<T>(YercCommandHeader header, byte[]? dataPart, Func<byte[], T> parser, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return RobotErrorMapper.ExecuteAsync(
            async token => parser(await ExchangePayloadAsync(header, dataPart, token).ConfigureAwait(false)),
            _options.Timeout,
            cancellationToken);
    }

    private Task<RobotResult> SendControlAsync(YercCommandHeader header, byte[]? dataPart, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return RobotErrorMapper.ExecuteAsync(
            async token => { await ExchangePayloadAsync(header, dataPart, token).ConfigureAwait(false); },
            _options.Timeout,
            cancellationToken);
    }

    private Task<byte[]> ExchangePayloadAsync(YercCommandHeader header, byte[]? dataPart, CancellationToken cancellationToken) =>
        _transport.RunExclusiveAsync(
            async session =>
            {
                byte requestId = NextRequestId();
                byte[] request = YercRequestBuilder.BuildRequest(header, requestId, dataPart);
                await session.SendAsync(request).ConfigureAwait(false);
                byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
                return YercResponseParser.ExtractPayload(response, requestId, header.Service);
            },
            cancellationToken);

    private byte NextRequestId()
    {
        lock (_requestIdLock)
        {
            _requestId++;
            return _requestId;
        }
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

    private static YercCommandHeader ParseAddressHeader(string address, bool isWrite)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        string upper = address.Trim().ToUpperInvariant();
        if (upper.Length >= MinimumAddressLength && ushort.TryParse(upper.Substring(1), out ushort instance))
        {
            byte service = isWrite ? (byte)YercService.SetSingle : (byte)YercService.GetSingle;
            return upper[0] switch
            {
                'M' => new YercCommandHeader((ushort)YercCommand.SingleRegister, instance, SinglePointAttribute, service),
                'B' => new YercCommandHeader((ushort)YercCommand.SingleByte, instance, SinglePointAttribute, service),
                'I' => new YercCommandHeader((ushort)YercCommand.SingleInteger, instance, SinglePointAttribute, service),
                'D' => new YercCommandHeader((ushort)YercCommand.SingleDoubleInteger, instance, SinglePointAttribute, service),
                'R' => new YercCommandHeader((ushort)YercCommand.SingleReal, instance, SinglePointAttribute, service),
                'S' => new YercCommandHeader((ushort)YercCommand.SingleString, instance, SinglePointAttribute, service),
                _ => throw new FormatException($"지원하지 않는 YERC 변수 주소입니다: '{address}'.")
            };
        }

        throw new FormatException($"유효하지 않은 YERC 변수 주소입니다: '{address}'.");
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("YASKAWA High-Speed UDP 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_lifecycleGate.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(YercUdpClient));
        }
    }
}
