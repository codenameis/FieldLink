using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Shared;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Fanuc.RobotInterface;

/// <summary>FANUC Robot Interface (SRTP/SNPX over TCP) 통신 클라이언트입니다.</summary>
public sealed partial class FanucClient : IRobotClient
{
    private const ushort StringRegisterReadWordCount = 40;
    private const int StringRegisterMaxByteCount = 80;
    private const int BytesPerWord = 2;
    private const string SnapshotPseudoAddress = "snapshot";
    private const string StringRegisterPrefix = "SR";

    private readonly ITransport _transport;
    private readonly FanucOptions _options;
    private readonly FanucFrameDecoder _frameDecoder = new();
    private readonly ProtocolLifecycleGate _lifecycleGate = new();
    private readonly Func<long> _timestampProvider;
    private readonly object _cacheLock = new();

    private long _initializedConnectionId = -1;
    private FanucSnapshot? _cachedSnapshot;
    private long _cacheTimestampTicks;
    private int _isOpen;

    /// <summary>전송 구현체와 옵션으로 FANUC 클라이언트를 초기화합니다.</summary>
    public FanucClient(ITransport transport, FanucOptions? options = null)
        : this(transport, options, Stopwatch.GetTimestamp)
    {
    }

    internal FanucClient(ITransport transport, FanucOptions? options, Func<long> timestampProvider)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new FanucOptions();
        _timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));
    }

    /// <inheritdoc />
    public bool IsOpen => !_lifecycleGate.IsDisposed && Volatile.Read(ref _isOpen) != 0 && _transport.State == TransportState.Open;

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
                if (!await _lifecycleGate.TryEnterAsync(token).ConfigureAwait(false))
                {
                    throw new ObjectDisposedException(nameof(FanucClient));
                }

                try
                {
                    ThrowIfDisposed();
                    if (IsOpen)
                    {
                        throw new InvalidOperationException("FANUC 클라이언트가 이미 열려 있습니다.");
                    }

                    await _transport.OpenAsync(token).ConfigureAwait(false);
                    if (_lifecycleGate.IsDisposed)
                    {
                        await CloseTransportBestEffortAsync().ConfigureAwait(false);
                        throw new ObjectDisposedException(nameof(FanucClient));
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
                    _initializedConnectionId = -1;
                    ClearCachedSnapshot();
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

    /// <inheritdoc />
    public Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default) =>
        ReadAddressWordsAsync(address, 1, cancellationToken);

    /// <inheritdoc />
    public Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        if (string.IsNullOrWhiteSpace(address) || string.Equals(address, SnapshotPseudoAddress, StringComparison.OrdinalIgnoreCase))
        {
            ThrowIfDisposed();
            EnsureOpen();

            return RobotErrorMapper.ExecuteAsync(
                async token =>
                {
                    FanucSnapshot snapshot = await ReadSnapshotCoreAsync(token).ConfigureAwait(false);
                    return snapshot.ToJson();
                },
                _options.Timeout,
                cancellationToken);
        }

        return ReadStringRegisterAsync(address, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default) =>
        WriteAddressWordsAsync(address, data, cancellationToken);

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

        byte[] encoded = _options.StringEncoding.GetBytes(value);
        bool isStringRegister = address.Trim().StartsWith(StringRegisterPrefix, StringComparison.OrdinalIgnoreCase);
        if (isStringRegister && encoded.Length > StringRegisterMaxByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                encoded.Length,
                $"FANUC SR 문자열은 최대 {StringRegisterMaxByteCount}바이트를 초과할 수 없습니다.");
        }

        if (encoded.Length == 0)
        {
            encoded = new byte[BytesPerWord];
        }
        else if (encoded.Length % BytesPerWord != 0)
        {
            Array.Resize(ref encoded, encoded.Length + 1);
        }
        else if (!isStringRegister || encoded.Length <= StringRegisterMaxByteCount - BytesPerWord)
        {
            // 짝수 길이 문자열도 컨트롤러의 기존 잔여 바이트를 덮어쓰도록 널 종료(2B)를 보장한다.
            Array.Resize(ref encoded, encoded.Length + BytesPerWord);
        }

        return WriteAddressWordsAsync(address, encoded, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_lifecycleGate.IsDisposed)
        {
            return;
        }

        Volatile.Write(ref _isOpen, 0);
        _initializedConnectionId = -1;
        ClearCachedSnapshot();
        _lifecycleGate.Dispose();
        _transport.Dispose();
    }

    private async Task<RobotResult<string>> ReadStringRegisterAsync(string address, CancellationToken cancellationToken)
    {
        RobotResult<byte[]> words = await ReadAddressWordsAsync(address, StringRegisterReadWordCount, cancellationToken).ConfigureAwait(false);
        return words.IsSuccess
            ? RobotResult.Success(RobotBinaryBuffer.ReadNullTrimmedString(words.Value, 0, words.Value.Length, _options.StringEncoding))
            : RobotResult.Failure<string>(words.Error);
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

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("FANUC 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_lifecycleGate.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(FanucClient));
        }
    }
}
