using FieldLink.Communication.Framing;
using System.Net;
using System.Threading.Tasks;

namespace FieldLink.Communication
{
    /// <summary>서버 콜백이 실행되는 동안만 유효한 연결입니다. 송수신은 순차적으로 await하세요.</summary>
    public interface ITcpServerConnection
    {
        /// <summary>접속한 상대 주소입니다.</summary>
        EndPoint RemoteEndPoint { get; }
        /// <summary>하나의 완전한 프레임을 받습니다. 다음 프레임의 잉여 바이트는 연결에 보관합니다.</summary>
        Task<byte[]> ReceiveAsync(IFrameBoundary boundary);
        /// <summary>바이트 전체를 전송합니다. 완료 전까지 배열을 수정하지 마세요.</summary>
        Task SendAsync(byte[] message);
    }
}
