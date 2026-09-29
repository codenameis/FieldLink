using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication.Tcp
{
    /// <summary>ExecuteTransactionAsync 콜백 안에서만 유효한 TCP 독점 교환 구간입니다.</summary>
    public interface ITcpTransaction
    {
        /// <summary>이 독점 작업이 사용하는 연결의 세대 번호입니다. 장치 초기화 결과가 같은 연결에 속하는지 확인합니다.</summary>
        long ConnectionGeneration { get; }
        /// <summary>전체 제한 시간, 호출자 취소, Close 또는 Dispose 시 취소됩니다. 콜백의 비동기 대기에도 전달하세요.</summary>
        CancellationToken CancellationToken { get; }
        /// <summary>메시지를 전송합니다. 장치의 처리 완료를 보장하지 않습니다.</summary>
        /// <remarks>입력은 복사하지 않습니다. 이 호출의 Task가 끝날 때까지 배열을 유지하고 다음 교환 전에 await하세요.</remarks>
        Task SendAsync(byte[] data);
        /// <summary>메시지 하나를 받습니다. 생략한 경계 규칙은 클라이언트의 기본값을 사용합니다.</summary>
        Task<byte[]> ReceiveAsync(IFrameBoundary boundary = null);
        /// <summary>명령과 응답을 교환합니다. 트랜잭션 전체의 마감 시간을 공유합니다.</summary>
        /// <remarks>요청은 복사하지 않습니다. 이 호출의 Task가 끝날 때까지 배열을 수정·재사용·풀 반환하지 마세요. 반환 응답은 독립 배열입니다.</remarks>
        Task<byte[]> ExchangeAsync(byte[] request, IFrameBoundary boundary = null,
            Func<byte[], ResponseDisposition> classifyResponse = null);
        /// <summary>현재 교환이 성공한 뒤 해당 연결만 닫도록 예약합니다. 단발 연결 프로토콜에서 사용하며 새 연결에는 영향을 주지 않습니다.</summary>
        void CloseAfterCompletion();
    }
}
