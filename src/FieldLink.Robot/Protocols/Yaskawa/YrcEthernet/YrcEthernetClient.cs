using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Shared;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

/// <summary>YASKAWA YRC1000 / DX200 Ethernet Server TCP 2단계 명령 클라이언트입니다.</summary>
public sealed partial class YrcEthernetClient : IRobotClient
{
    private const int MaximumLineLength = 8192;
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;
    private const string ErrorPrefix = "ERROR:";

    private static readonly char[] AddressSeparators = { '.', ':', ';' };

    private readonly ITransport _transport;
    private readonly YrcEthernetOptions _options;
    private readonly DelimitedFrameDecoder _crLfDecoder = new(new[] { CarriageReturn, LineFeed }, MaximumLineLength);
    private readonly DelimitedFrameDecoder _crDecoder = new(new[] { CarriageReturn }, MaximumLineLength);
    private readonly FixedLengthFrameDecoder _singleByteDecoder = new(1);
    private readonly ProtocolLifecycleGate _lifecycleGate = new();

    private long _connectedConnectionId = -1;
    private int _isPersistentConnection;
    private int _isOpen;

    /// <summary>전송 구현체와 옵션으로 YRC Ethernet TCP 클라이언트를 초기화합니다.</summary>
    public YrcEthernetClient(ITransport transport, YrcEthernetOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new YrcEthernetOptions();
    }

    /// <summary>현재 세션이 영구 연결(Keep-Alive:-1)로 협상되었는지 여부입니다.</summary>
    public bool IsPersistentConnection => Volatile.Read(ref _isPersistentConnection) != 0;

    /// <inheritdoc />
    public bool IsOpen
    {
        get
        {
            if (_lifecycleGate.IsDisposed || Volatile.Read(ref _isOpen) == 0)
            {
                return false;
            }

            TransportState state = _transport.State;
            return state == TransportState.Open || (!IsPersistentConnection && state == TransportState.Closed);
        }
    }

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("YRC Ethernet 클라이언트가 이미 열려 있습니다.");
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                if (!await _lifecycleGate.TryEnterAsync(token).ConfigureAwait(false))
                {
                    throw new ObjectDisposedException(nameof(YrcEthernetClient));
                }

                try
                {
                    ThrowIfDisposed();
                    if (IsOpen)
                    {
                        throw new InvalidOperationException("YRC Ethernet 클라이언트가 이미 열려 있습니다.");
                    }

                    await _transport.OpenAsync(token).ConfigureAwait(false);
                    if (_lifecycleGate.IsDisposed)
                    {
                        await CloseTransportBestEffortAsync().ConfigureAwait(false);
                        throw new ObjectDisposedException(nameof(YrcEthernetClient));
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
                    Volatile.Write(ref _isPersistentConnection, 0);
                    _connectedConnectionId = -1;
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

    /// <summary>2단계 프로토콜로 임의 YRC 명령과 데이터를 실행합니다.</summary>
    public Task<RobotResult<string>> ExecuteCommandAsync(
        string command,
        string? commandData = null,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("명령 이름은 비어 있을 수 없습니다.", nameof(command));
        }

        YrcEthernetRequestBuilder.ValidateNoLineBreaks(command, nameof(command));
        byte[]? dataBytes = commandData is null ? null : _options.Encoding.GetBytes(commandData);
        if (dataBytes is not null)
        {
            _ = YrcEthernetRequestBuilder.BuildPhase2Payload(dataBytes);
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            token => ExecuteTwoPhaseAsync(command, dataBytes, token),
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default)
    {
        RobotResult<string> result = await ReadStringAsync(address, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? RobotResult.Success(_options.Encoding.GetBytes(result.Value))
            : RobotResult.Failure<byte[]>(result.Error);
    }

    /// <inheritdoc />
    public Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        int separatorIndex = address.IndexOfAny(AddressSeparators);
        if (separatorIndex >= 0)
        {
            string command = address.Substring(0, separatorIndex);
            string data = address.Substring(separatorIndex + 1);
            return ExecuteCommandAsync(command, data, cancellationToken);
        }

        return ExecuteCommandAsync(address, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return WriteStringAsync(address, _options.Encoding.GetString(data), cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteStringAsync(string address, string value, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return ExecuteControlAsync(address, value.Length == 0 ? null : value, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_lifecycleGate.IsDisposed)
        {
            return;
        }

        Volatile.Write(ref _isOpen, 0);
        Volatile.Write(ref _isPersistentConnection, 0);
        _connectedConnectionId = -1;
        _lifecycleGate.Dispose();
        _transport.Dispose();
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("YRC Ethernet 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_lifecycleGate.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(YrcEthernetClient));
        }
    }
}
