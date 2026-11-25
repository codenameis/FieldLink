using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Transport.Framing;

namespace FieldLink.Transport;

/// <summary>스트림 기반 연결(TCP 소켓, 시리얼 포트 등)에서 프레임을 송수신하는 세션입니다.</summary>
internal sealed class StreamTransportSession : ITransportSession
{
    private const int DefaultInitialBufferSize = 8192;
    private const int MaximumBufferSize = 65536;
    private const int BufferGrowthFactor = 2;

    private readonly Stream _stream;
    private readonly Action? _onBeforeSend;
    private readonly Action? _onReadAbandoned;
    private byte[] _buffer;
    private int _bufferedCount;
    private bool _hasSent;
    private bool _isAborted;

    public StreamTransportSession(
        long connectionId,
        Stream stream,
        CancellationToken cancellationToken,
        int initialBufferSize = DefaultInitialBufferSize,
        Action? onBeforeSend = null,
        Action? onReadAbandoned = null)
    {
        ConnectionId = connectionId;
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        CancellationToken = cancellationToken;
        _onBeforeSend = onBeforeSend;
        _onReadAbandoned = onReadAbandoned;
        int clampedBufferSize = Math.Min(Math.Max(initialBufferSize, DefaultInitialBufferSize), MaximumBufferSize);
        _buffer = new byte[clampedBufferSize];
        _bufferedCount = 0;
    }

    public long ConnectionId { get; }

    public CancellationToken CancellationToken { get; private set; }

    public void BeginExclusiveExchange(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
        _hasSent = false;
    }

    public void ClearReceiveBuffer()
    {
        if (_bufferedCount > 0)
        {
            Array.Clear(_buffer, 0, _bufferedCount);
            _bufferedCount = 0;
        }
    }

    public async Task SendAsync(byte[] frame)
    {
        if (frame is null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        CancellationToken.ThrowIfCancellationRequested();
        ThrowIfAborted(isRequestSent: false);

        if (frame.Length == 0)
        {
            return;
        }

        ClearReceiveBuffer();
        _onBeforeSend?.Invoke();

        try
        {
            await _stream.WriteAsync(frame, 0, frame.Length, CancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(CancellationToken).ConfigureAwait(false);
            _hasSent = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "전송 연결이 닫혀 프레임을 전송할 수 없습니다.",
                isRequestSent: false,
                innerException: exception);
        }
        catch (Exception exception)
        {
            throw new TransportException(
                TransportFailure.SendFailed,
                "프레임을 전송하는 중 입출력 오류가 발생했습니다.",
                isRequestSent: true,
                innerException: exception);
        }
    }

    public async Task<byte[]> ReceiveAsync(IFrameDecoder decoder)
    {
        if (decoder is null)
        {
            throw new ArgumentNullException(nameof(decoder));
        }

        while (true)
        {
            CancellationToken.ThrowIfCancellationRequested();
            ThrowIfAborted(isRequestSent: _hasSent);

            if (_bufferedCount > 0)
            {
                var segment = new ArraySegment<byte>(_buffer, 0, _bufferedCount);
                if (decoder.TryGetFrameLength(segment, out int frameLength))
                {
                    return ExtractFrame(frameLength);
                }
            }

            EnsureCapacity();
            int bytesRead = await ReadNextChunkAsync().ConfigureAwait(false);
            _bufferedCount += bytesRead;
        }
    }

    private async Task<int> ReadNextChunkAsync()
    {
        CancellationToken.ThrowIfCancellationRequested();
        ThrowIfAborted(isRequestSent: _hasSent);

        int bytesRead;
        try
        {
            Task<int> readTask = _stream.ReadAsync(
                _buffer,
                _bufferedCount,
                _buffer.Length - _bufferedCount,
                CancellationToken);

            if (!readTask.IsCompleted && CancellationToken.CanBeCanceled)
            {
                var cancellationSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (CancellationToken.Register(
                    static state =>
                    {
                        if (state is TaskCompletionSource<bool> signal)
                        {
                            signal.TrySetResult(true);
                        }
                    },
                    cancellationSignal))
                {
                    Task completedTask = await Task.WhenAny(readTask, cancellationSignal.Task).ConfigureAwait(false);
                    if (completedTask != readTask)
                    {
                        // netstandard2.0 스트림은 취소 토큰을 무시하고 OS I/O를 계속 점유하므로,
                        // 고아 ReadAsync가 후속 요청 응답을 공유 _buffer에 덮어쓰지 않도록 연결을 즉시 중단한다.
                        TransportTaskWait.ObserveFaultSafely(readTask);
                        AbortOnOrphanedRead();
                        CancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }

            bytesRead = await readTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException exception)
        {
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "전송 연결이 닫혀 프레임을 수신할 수 없습니다.",
                isRequestSent: _hasSent,
                innerException: exception);
        }
        catch (Exception exception)
        {
            throw new TransportException(
                TransportFailure.ReceiveFailed,
                "프레임을 수신하는 중 입출력 오류가 발생했습니다.",
                isRequestSent: _hasSent,
                innerException: exception);
        }

        if (bytesRead == 0)
        {
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "원격 호스트에서 연결이 닫혔습니다.",
                isRequestSent: _hasSent);
        }

        return bytesRead;
    }

    private void ThrowIfAborted(bool isRequestSent)
    {
        if (_isAborted)
        {
            throw new TransportException(
                TransportFailure.ConnectionClosed,
                "이전 수신 작업이 취소되어 연결이 중단되었습니다.",
                isRequestSent: isRequestSent);
        }
    }

    private void AbortOnOrphanedRead()
    {
        _isAborted = true;
        _bufferedCount = 0;

        if (_onReadAbandoned is not null)
        {
            _onReadAbandoned();
            return;
        }

        try
        {
            _stream.Dispose();
        }
        catch
        {
            // 고아 I/O 중단을 위한 스트림 정리 예외는 무시
        }
    }

    private byte[] ExtractFrame(int frameLength)
    {
        if (frameLength <= 0 || frameLength > _bufferedCount || frameLength > MaximumBufferSize)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"디코더가 유효하지 않은 프레임 길이({frameLength})를 반환했습니다. (버퍼 바이트: {_bufferedCount})",
                isRequestSent: _hasSent);
        }

        byte[] frame = new byte[frameLength];
        Array.Copy(_buffer, 0, frame, 0, frameLength);

        int remainingBytes = _bufferedCount - frameLength;
        if (remainingBytes > 0)
        {
            Array.Copy(_buffer, frameLength, _buffer, 0, remainingBytes);
        }

        Array.Clear(_buffer, remainingBytes, frameLength);
        _bufferedCount = remainingBytes;
        return frame;
    }

    private void EnsureCapacity()
    {
        if (_bufferedCount < _buffer.Length)
        {
            return;
        }

        if (_buffer.Length >= MaximumBufferSize)
        {
            throw new TransportException(
                TransportFailure.InvalidFrame,
                $"수신 버퍼 최대 크기({MaximumBufferSize} 바이트)에 도달할 때까지 완성된 프레임을 감지하지 못했습니다.",
                isRequestSent: _hasSent);
        }

        int newCapacity = Math.Min(_buffer.Length * BufferGrowthFactor, MaximumBufferSize);
        byte[] expandedBuffer = new byte[newCapacity];
        Array.Copy(_buffer, 0, expandedBuffer, 0, _bufferedCount);
        _buffer = expandedBuffer;
    }
}
