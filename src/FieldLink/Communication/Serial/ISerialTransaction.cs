using FieldLink.Communication.Framing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.Serial
{
    /// <summary>콜백이 실행되는 동안만 사용할 수 있는 시리얼 독점 교환입니다. 각 작업은 순서대로 await하세요.</summary>
    public interface ISerialTransaction
    {
        /// <summary>이 작업이 사용하는 포트의 연결 세대입니다.</summary>
        long ConnectionGeneration { get; }
        /// <summary>전체 제한 시간·취소·포트 종료를 나타내는 토큰입니다.</summary>
        CancellationToken CancellationToken { get; }
        /// <summary>바이트를 전송합니다. 완료 전까지 입력 배열을 수정하지 마세요.</summary>
        Task SendAsync(byte[] message);
        /// <summary>전송 없이 다음 프레임을 수신합니다.</summary>
        Task<byte[]> ReceiveAsync(IFrameBoundary boundary = null);
        /// <summary>전송 후 하나의 응답 프레임을 받습니다. 배열 소유권은 SendAsync와 같습니다.</summary>
        Task<byte[]> ExchangeAsync(byte[] request, IFrameBoundary boundary = null,
            Func<byte[], ResponseDisposition> classifyResponse = null);
        /// <summary>현재 수신 버퍼와 운영체제 입력 버퍼를 비웁니다.</summary>
        void DiscardInput();
    }
}
