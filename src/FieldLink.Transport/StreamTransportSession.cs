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

    private readonly Stream _stream;
    private byte[] _buffer;
    private int _bufferedCount;

    public StreamTransportSession(
        long connectionId,
        Stream stream,
        CancellationToken cancellationToken,
        int initialBufferSize = DefaultInitialBufferSize)
    {
        ConnectionId = connectionId;
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        CancellationToken = cancellationToken;
        _buffer = new byte[Math.Max(initialBufferSize, DefaultInitialBufferSize)];
        _bufferedCount = 0;
    }

    public long ConnectionId { get; }

    public CancellationToken CancellationToken { get; }

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

        try
        {
            await _stream.WriteAsync(frame, 0, frame.Length, CancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(CancellationToken).ConfigureAwait(false);
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

            int bytesRead;
            try
            {
                bytesRead = await _stream.ReadAsync(
                    _buffer,
                    _bufferedCount,
                    _buffer.Length - _bufferedCount,
                    CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new TransportException(
                    TransportFailure.ReceiveFailed,
                    "프레임을 수신하는 중 입출력 오류가 발생했습니다.",
                    isRequestSent: true,
                    innerException: exception);
            }

            if (bytesRead == 0)
            {
                throw new TransportException(
                    TransportFailure.ConnectionClosed,
                    "원격 호스트에서 연결이 닫혔습니다.",
                    isRequestSent: true);
            }

            _bufferedCount += bytesRead;
        }
    }

    private byte[] ExtractFrame(int frameLength)
    {
        byte[] frame = new byte[frameLength];
        Array.Copy(_buffer, 0, frame, 0, frameLength);

        int remainingBytes = _bufferedCount - frameLength;
        if (remainingBytes > 0)
        {
            Array.Copy(_buffer, frameLength, _buffer, 0, remainingBytes);
        }

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
                $"수신 버퍼 최대 크기({MaximumBufferSize} 바이트)에 도달할 때까지 완성된 프레임을 감지하지 못했습니다.");
        }

        int newCapacity = Math.Min(_buffer.Length * 2, MaximumBufferSize);
        byte[] expandedBuffer = new byte[newCapacity];
        Array.Copy(_buffer, 0, expandedBuffer, 0, _bufferedCount);
        _buffer = expandedBuffer;
    }
}
