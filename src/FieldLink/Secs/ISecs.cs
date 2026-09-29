using System.Threading;
using System.Threading.Tasks;
using FieldLink.Secs.Types;

namespace FieldLink.Secs
{
    /// <summary>GEM 명령이 사용하는 SECS 요청·응답 계약입니다.</summary>
    public interface ISecs
    {
        /// <summary>W=1인 primary를 보내고 같은 System Bytes의 secondary를 기다립니다.</summary>
        Task<SecsMessage> RequestAsync(byte stream, byte function, SecsValue data,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
