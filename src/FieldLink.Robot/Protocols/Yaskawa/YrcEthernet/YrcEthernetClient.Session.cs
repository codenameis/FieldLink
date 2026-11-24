using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;

public sealed partial class YrcEthernetClient
{
    private Task<RobotResult<T>> ExecuteCommandAndParseAsync<T>(
        string command,
        string? commandData,
        Func<string, T> parser,
        CancellationToken cancellationToken)
    {
        YrcEthernetRequestBuilder.ValidateNoLineBreaks(command, nameof(command));
        byte[]? dataBytes = commandData is null ? null : _options.Encoding.GetBytes(commandData);
        if (dataBytes is not null)
        {
            _ = YrcEthernetRequestBuilder.BuildPhase2Payload(dataBytes);
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                string raw = await ExecuteTwoPhaseAsync(command, dataBytes, token).ConfigureAwait(false);
                return parser(raw);
            },
            _options.Timeout,
            cancellationToken);
    }

    private async Task<RobotResult> ExecuteControlAsync(string command, string? data, CancellationToken cancellationToken)
    {
        RobotResult<string> result = await ExecuteCommandAsync(command, data, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? RobotResult.Success() : RobotResult.Failure(result.Error);
    }

    private async Task<string> ExecuteTwoPhaseAsync(string command, byte[]? dataBytes, CancellationToken cancellationToken)
    {
        if (!await _lifecycleGate.TryEnterAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new ObjectDisposedException(nameof(YrcEthernetClient));
        }

        bool succeeded = false;
        try
        {
            ThrowIfDisposed();
            if (_transport.State == TransportState.Closed && Volatile.Read(ref _isOpen) != 0)
            {
                await _transport.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            string response = await _transport.RunExclusiveAsync(
                session => SendAndReceiveTwoPhaseAsync(session, command, dataBytes),
                cancellationToken).ConfigureAwait(false);
            succeeded = true;
            return response;
        }
        finally
        {
            if (!IsPersistentConnection)
            {
                _connectedConnectionId = -1;
                if (succeeded)
                {
                    await _transport.CloseAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await CloseTransportBestEffortAsync().ConfigureAwait(false);
                }
            }

            _lifecycleGate.Exit();
        }
    }

    private async Task<string> SendAndReceiveTwoPhaseAsync(ITransportSession session, string command, byte[]? dataBytes)
    {
        await EnsureConnectHandshakeAsync(session).ConfigureAwait(false);

        byte[] phase1 = YrcEthernetRequestBuilder.BuildPhase1Header(command, dataBytes, _options.Encoding);
        await session.SendAsync(phase1).ConfigureAwait(false);
        byte[] phase1ReplyBytes = await session.ReceiveAsync(_crLfDecoder).ConfigureAwait(false);
        YrcEthernetResponseParser.ValidatePhase1Ack(
            YrcEthernetResponseParser.DecodeLine(phase1ReplyBytes, _options.Encoding, stripCrLf: true));

        if (dataBytes is { Length: > 0 })
        {
            await session.SendAsync(YrcEthernetRequestBuilder.BuildPhase2Payload(dataBytes)).ConfigureAwait(false);
        }

        byte[] phase3Bytes = await session.ReceiveAsync(_crDecoder).ConfigureAwait(false);
        string rawLine = YrcEthernetResponseParser.DecodeLine(phase3Bytes, _options.Encoding, stripCrLf: false);
        string parsed = YrcEthernetResponseParser.ParsePhase3Result(rawLine, out bool shouldDrainNewline);

        if (shouldDrainNewline)
        {
            byte[] drained = await session.ReceiveAsync(_singleByteDecoder).ConfigureAwait(false);
            if (drained.Length != 1 || drained[0] != LineFeed)
            {
                throw new RobotException(
                    RobotErrorKind.InvalidResponse,
                    $"YRC Phase 3 응답 뒤 기대한 LF(0x{LineFeed:X2}) 대신 0x{(drained.Length > 0 ? drained[0] : 0):X2}가 수신되었습니다.",
                    drained);
            }
        }

        if (rawLine.StartsWith(ErrorPrefix, StringComparison.Ordinal))
        {
            throw YrcEthernetResponseParser.ParseErrorResultException(rawLine);
        }

        return parsed;
    }

    private async Task EnsureConnectHandshakeAsync(ITransportSession session)
    {
        if (_connectedConnectionId == session.ConnectionId)
        {
            return;
        }

        await session.SendAsync(YrcEthernetRequestBuilder.BuildConnectRequest(_options.Encoding)).ConfigureAwait(false);
        byte[] replyBytes = await session.ReceiveAsync(_crLfDecoder).ConfigureAwait(false);
        string reply = YrcEthernetResponseParser.DecodeLine(replyBytes, _options.Encoding, stripCrLf: true);
        Volatile.Write(ref _isPersistentConnection, YrcEthernetResponseParser.ParseConnectResponse(reply) ? 1 : 0);
        _connectedConnectionId = session.ConnectionId;
    }

    private async Task CloseTransportBestEffortAsync()
    {
        try
        {
            if (_transport.State == TransportState.Open)
            {
                await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // 비영구 연결 정리 중 발생한 예외가 원래 통신 실패를 가리지 않도록 무시한다.
        }
    }
}
