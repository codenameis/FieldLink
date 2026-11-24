using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Robot.Protocols.Shared;
using FieldLink.Transport;
using FieldLink.Transport.Framing;

namespace FieldLink.Robot.Protocols.Yamaha.Rcx;

/// <summary>YAMAHA RCX 시리즈 온라인 명령 TCP ASCII 클라이언트입니다.</summary>
public sealed partial class YamahaRcxClient : IRobotClient
{
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;
    private const int SingleLineResponse = 1;
    private const int TwoLineResponse = 2;

    private readonly ITransport _transport;
    private readonly YamahaRcxOptions _options;
    private readonly DelimitedFrameDecoder _lineDecoder;
    private readonly ProtocolLifecycleGate _lifecycleGate = new();

    private int _isOpen;

    /// <summary>전송 구현체와 옵션으로 YAMAHA RCX 클라이언트를 초기화합니다.</summary>
    public YamahaRcxClient(ITransport transport, YamahaRcxOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new YamahaRcxOptions();
        _lineDecoder = new DelimitedFrameDecoder(new[] { CarriageReturn, LineFeed }, _options.MaximumLineLength);
    }

    /// <inheritdoc />
    public bool IsOpen => !_lifecycleGate.IsDisposed && Volatile.Read(ref _isOpen) != 0 && _transport.State == TransportState.Open;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("YAMAHA RCX 클라이언트가 이미 열려 있습니다.");
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                if (!await _lifecycleGate.TryEnterAsync(token).ConfigureAwait(false))
                {
                    throw new ObjectDisposedException(nameof(YamahaRcxClient));
                }

                try
                {
                    ThrowIfDisposed();
                    if (IsOpen)
                    {
                        throw new InvalidOperationException("YAMAHA RCX 클라이언트가 이미 열려 있습니다.");
                    }

                    await _transport.OpenAsync(token).ConfigureAwait(false);
                    if (_lifecycleGate.IsDisposed)
                    {
                        await CloseTransportBestEffortAsync().ConfigureAwait(false);
                        throw new ObjectDisposedException(nameof(YamahaRcxClient));
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

    /// <summary>온라인 명령을 1회 송신하고 지정된 줄 수만큼 연속으로 응답 라인을 수신합니다.</summary>
    public Task<RobotResult<string[]>> SendCommandAsync(
        string command,
        int expectedLines = SingleLineResponse,
        CancellationToken cancellationToken = default)
    {
        byte[] request = YamahaRcxRequestBuilder.BuildCommand(command);
        return ExchangeLinesAsync(request, expectedLines, cancellationToken);
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

    private Task<RobotResult> SendOkCommandAsync(byte[] request, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                string[] lines = await ExchangeLinesCoreAsync(request, expectedLines: SingleLineResponse, token).ConfigureAwait(false);
                YamahaRcxResponseParser.ValidateOkLine(lines[0]);
            },
            _options.Timeout,
            cancellationToken);
    }

    private Task<RobotResult> SendTwoLineOkCommandAsync(byte[] request, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                string[] lines = await ExchangeLinesCoreAsync(request, expectedLines: TwoLineResponse, token).ConfigureAwait(false);
                YamahaRcxResponseParser.ValidateOkLine(lines[1]);
            },
            _options.Timeout,
            cancellationToken);
    }

    private Task<RobotResult<string[]>> ExchangeLinesAsync(
        byte[] request,
        int expectedLines,
        CancellationToken cancellationToken)
    {
        if (expectedLines <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedLines), expectedLines, "기대 응답 줄 수는 1 이상이어야 합니다.");
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            token => ExchangeLinesCoreAsync(request, expectedLines, token),
            _options.Timeout,
            cancellationToken);
    }

    private Task<string[]> ExchangeLinesCoreAsync(byte[] request, int expectedLines, CancellationToken cancellationToken) =>
        _transport.RunExclusiveAsync(
            async session =>
            {
                // 사양서 07 §3.2 & §9.1: 명령은 1회만 송신하고 기대 라인 수만큼 연속 수신
                await session.SendAsync(request).ConfigureAwait(false);

                var lines = new List<string>(expectedLines);
                for (int i = 0; i < expectedLines; i++)
                {
                    byte[] rawLine = await session.ReceiveAsync(_lineDecoder).ConfigureAwait(false);
                    string line = YamahaRcxResponseParser.StripLineTerminator(rawLine);
                    lines.Add(line);

                    // 첫 줄이 NG 오류이면 후속 줄 대기 타임아웃을 피하기 위해 즉시 조기 실패 처리
                    if (i < expectedLines - 1 && YamahaRcxResponseParser.IsErrorLine(line))
                    {
                        throw new RobotDeviceException(0, line, rawLine);
                    }
                }

                return lines.ToArray();
            },
            cancellationToken);

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
            throw new InvalidOperationException("YAMAHA RCX 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_lifecycleGate.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(YamahaRcxClient));
        }
    }
}
