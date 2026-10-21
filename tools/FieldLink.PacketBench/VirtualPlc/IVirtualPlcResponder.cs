using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>가상 PLC 가 흉내 내는 프로토콜 하나입니다. 프레임 경계 판정과 요청 처리를 맡습니다.</summary>
internal interface IVirtualPlcResponder
{
    /// <summary>화면에 보여 줄 프로토콜 이름입니다.</summary>
    string DisplayName { get; }

    /// <summary>기본 포트 번호입니다.</summary>
    int DefaultPort { get; }

    /// <summary>요청 프레임 하나를 끝까지 읽습니다. 연결이 끊기면 null 입니다.</summary>
    Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken);

    /// <summary>요청 프레임을 처리합니다. 응답하지 않아야 하면 null 을 돌려줍니다.</summary>
    byte[]? Process(byte[] request, VirtualPlcSettings settings);
}
