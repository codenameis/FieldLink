using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>Modbus TCP 의 서버 역할입니다. MBAP 헤더의 길이 필드(빅 엔디언)로 프레임을 자릅니다.</summary>
internal sealed class ModbusTcpResponder : IVirtualPlcResponder
{
    private const int MbapHeaderLength = 6;
    private const int MaxFrameLength = 260;

    public ModbusTcpResponder(VirtualPlcMemory memory)
    {
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    public VirtualPlcMemory Memory { get; }

    public string DisplayName => "Modbus TCP";

    public int DefaultPort => 1502;

    public async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[MbapHeaderLength];
        if (!await FrameIo.ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        int length = (header[4] << 8) | header[5];
        if (length is < 2 or > MaxFrameLength)
        {
            return null;
        }

        var frame = new byte[MbapHeaderLength + length];
        Buffer.BlockCopy(header, 0, frame, 0, MbapHeaderLength);

        if (!await FrameIo.ReadExactlyAsync(stream, frame.AsMemory(MbapHeaderLength), cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return frame;
    }

    public byte[]? Process(byte[] request, VirtualPlcSettings settings)
        => ModbusRequestProcessor.Process(request, Memory, settings);
}
