using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Kuka.VarProxy;

/// <summary>KUKA KRC4 <c>KUKAVARPROXY</c> 바이너리 TCP 프로토콜 클라이언트입니다.</summary>
public sealed class KukaVarProxyClient : IRobotClient
{
    private readonly ITransport _transport;
    private readonly KukaVarProxyOptions _options;
    private readonly KukaVarProxyFrameDecoder _frameDecoder = new();
    private readonly object _messageIdLock = new();

    private ushort _messageId;
    private long _lastConnectionId = -1;
    private bool _isOpen;
    private bool _isDisposed;

    /// <summary>전송 구현체와 옵션으로 KUKAVARPROXY 클라이언트를 초기화합니다.</summary>
    public KukaVarProxyClient(ITransport transport, KukaVarProxyOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new KukaVarProxyOptions();
    }

    /// <inheritdoc />
    public bool IsOpen => !_isDisposed && _isOpen && _transport.State == TransportState.Open;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("KUKAVARPROXY 클라이언트가 이미 열려 있습니다.");
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
                await _transport.CloseAsync(token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>지정 KRL 변수의 원시 바이트 값을 읽습니다.</summary>
    public Task<RobotResult<byte[]>> ReadVariableRawAsync(string variableName, CancellationToken cancellationToken = default)
    {
        if (variableName is null)
        {
            throw new ArgumentNullException(nameof(variableName));
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            token => _transport.RunExclusiveAsync(
                async session =>
                {
                    ushort messageId = NextMessageId(session.ConnectionId);
                    byte[] request = KukaVarProxyRequestBuilder.BuildReadRequest(messageId, variableName, _options.Encoding);
                    await session.SendAsync(request).ConfigureAwait(false);
                    byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
                    return KukaVarProxyResponseParser.ParseValueBytes(response, messageId, KukaVarProxyFunctionCode.Read);
                },
                token),
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>지정 KRL 변수의 문자열 값을 읽습니다.</summary>
    public async Task<RobotResult<string>> ReadVariableAsync(string variableName, CancellationToken cancellationToken = default)
    {
        RobotResult<byte[]> raw = await ReadVariableRawAsync(variableName, cancellationToken).ConfigureAwait(false);
        return raw.IsSuccess
            ? RobotResult.Success(_options.Encoding.GetString(raw.Value))
            : RobotResult.Failure<string>(raw.Error);
    }

    /// <summary>지정 KRL 변수에 문자열 값을 쓰고 컨트롤러가 에코한 반영 값을 반환합니다.</summary>
    public Task<RobotResult<string>> WriteVariableAsync(
        string variableName,
        string value,
        CancellationToken cancellationToken = default)
    {
        if (variableName is null)
        {
            throw new ArgumentNullException(nameof(variableName));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            token => _transport.RunExclusiveAsync(
                async session =>
                {
                    ushort messageId = NextMessageId(session.ConnectionId);
                    byte[] request = KukaVarProxyRequestBuilder.BuildWriteRequest(messageId, variableName, value, _options.Encoding);
                    await session.SendAsync(request).ConfigureAwait(false);
                    byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
                    byte[] echoed = KukaVarProxyResponseParser.ParseValueBytes(response, messageId, KukaVarProxyFunctionCode.Write);
                    return _options.Encoding.GetString(echoed);
                },
                token),
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default) =>
        ReadVariableRawAsync(address, cancellationToken);

    /// <inheritdoc />
    public Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default) =>
        ReadVariableAsync(address, cancellationToken);

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
    public async Task<RobotResult> WriteStringAsync(string address, string value, CancellationToken cancellationToken = default)
    {
        RobotResult<string> result = await WriteVariableAsync(address, value, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? RobotResult.Success() : RobotResult.Failure(result.Error);
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

    private ushort NextMessageId(long connectionId)
    {
        lock (_messageIdLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _messageId = 0;
            }

            _messageId = (ushort)(_messageId == ushort.MaxValue ? 1 : _messageId + 1);
            return _messageId;
        }
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("KUKAVARPROXY 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(KukaVarProxyClient));
        }
    }
}
