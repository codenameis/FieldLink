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
    private byte[] _buffer;
    private int _bufferedCount;
    private bool _hasSent;

    public StreamTransportSession(
        long connectionId,
        Stream stream,
        CancellationToken cancellationToken,
        int initialBufferSize = DefaultInitialBufferSize,
        Action? onBeforeSend = null)
    {
        ConnectionId = connectionId;
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        CancellationToken = cancellationToken;
        _onBeforeSend = onBeforeSend;
        int clampedBufferSize = Math.Min(Math.Max(initialBufferSize, DefaultInitialBufferSize), MaximumBufferSize);
        _buffer = new byte[clampedBufferSize];
        _bufferedCount = 0;
    }

    public long ConnectionId { get; }

    public CancellationToken CancellationToken { get; private set; }

    public void SetCancellationToken(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
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
                var cancellationSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (CancellationToken.Register(
                    static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                    cancellationSource))
                {
                    Task completedTask = await Task.WhenAny(readTask, cancellationSource.Task).ConfigureAwait(false);
                    if (completedTask != readTask)
                    {
                        _ = readTask.ContinueWith(
                            static faulted => _ = faulted.Exception,
                            CancellationToken.None,
                            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default);
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
