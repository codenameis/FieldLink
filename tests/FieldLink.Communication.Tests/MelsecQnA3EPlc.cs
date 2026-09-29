using System.Net.Sockets;
using FieldLink.PlcDrivers.Melsec;

namespace FieldLink.Communication.Tests;

/// <summary>QnA3E 바이너리의 D 워드 일괄 읽기만 제공하는 테스트용 가상 PLC입니다.</summary>
internal static class MelsecQnA3EPlc
{
    internal sealed class Exchange
    {
        internal Exchange(byte[] request, byte[] response) { Request = request; Response = response; }
        internal byte[] Request { get; }
        internal byte[] Response { get; }
    }

    internal static async Task<Exchange> ServeOnceAsync(Socket peer, ushort[] dRegisters, CancellationToken cancellationToken)
    {
        // 클라이언트의 명령 빌더를 사용하지 않고 수신 바이트의 필드 위치를 직접 읽는다.
        byte[] header = await ReceiveExactlyAsync(peer, 9, cancellationToken);
        if (!header.Take(7).SequenceEqual(new byte[] { 0x50, 0, 0, 0xFF, 0xFF, 3, 0 }))
            throw new InvalidDataException("가상 PLC는 3E 바이너리 기본 경로(00/FF/03FF/00)만 지원합니다.");

        int bodyLength = ReadWord(header, 7);
        if (bodyLength < 2 || bodyLength > 1024)
            throw new InvalidDataException($"잘못된 요청 길이: {bodyLength}");
        byte[] body = await ReceiveExactlyAsync(peer, bodyLength, cancellationToken);
        byte[] request = [..header, ..body];
        byte[] response = BuildReadResponse(request, dRegisters);

        int sent = 0;
        while (sent < response.Length)
        {
            int count = await peer.SendAsync(new ArraySegment<byte>(response, sent, response.Length - sent), SocketFlags.None, cancellationToken);
            if (count == 0)
                throw new EndOfStreamException("PLC 응답을 전송하는 중 연결이 닫혔습니다.");
            sent += count;
        }
        return new Exchange(request, response);
    }

    private static byte[] BuildReadResponse(byte[] request, ushort[] dRegisters)
    {
        if (request.Length != 21)
            return Pack(0xC061, []);
        ushort command = ReadWord(request, 11);
        ushort subcommand = ReadWord(request, 13);
        if (command != 0x0401 || subcommand != 0)
            return Pack(0xC059, []);
        if (request[18] != 0xA8)
            return Pack(0xC05A, []); // D 디바이스만 제공

        int start = request[15] | request[16] << 8 | request[17] << 16;
        int points = ReadWord(request, 19);
        if (points < 1 || points > 960)
            return Pack(0xC051, []);
        if (start + points > dRegisters.Length)
            return Pack(0xC056, []);

        byte[] data = new byte[points * 2];
        for (int index = 0; index < points; index++)
        {
            ushort value = dRegisters[start + index];
            data[index * 2] = (byte)value;
            data[index * 2 + 1] = (byte)(value >> 8);
        }
        return Pack(0, data);
    }

    private static byte[] Pack(ushort status, byte[] data) =>
        MelsecMcServerCommandBuilder.PackCommand(new McResponseOptions { isBinary = true }, status, data);

    // 기존 BinaryPrimitives 대신 고정 위치의 리틀 엔디언 워드를 직접 읽는다.
    private static ushort ReadWord(byte[] data, int offset) => (ushort)(data[offset] | data[offset + 1] << 8);

    private static async Task<byte[]> ReceiveExactlyAsync(Socket socket, int length, CancellationToken cancellationToken)
    {
        byte[] bytes = new byte[length];
        int received = 0;
        while (received < length)
        {
            int count = await socket.ReceiveAsync(new ArraySegment<byte>(bytes, received, bytes.Length - received), SocketFlags.None, cancellationToken);
            if (count == 0)
                throw new EndOfStreamException($"필요한 길이 {length} 중 {received}바이트에서 연결이 닫혔습니다.");
            received += count;
        }
        return bytes;
    }
}
