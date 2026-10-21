using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>소켓에서 정해진 길이만큼 정확히 읽습니다.</summary>
internal static class FrameIo
{
    public static async Task<bool> ReadExactlyAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int received = 0;
        while (received < buffer.Length)
        {
            int count = await stream.ReadAsync(buffer[received..], cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return false;
            }

            received += count;
        }

        return true;
    }
}
