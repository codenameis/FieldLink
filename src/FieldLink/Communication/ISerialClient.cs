using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication
{
    /// <summary>시리얼 포트의 명시적 열기와 순차 메시지 교환 계약입니다.</summary>
    public interface ISerialClient : ICommunicationClient
    {
        /// <summary>성공적으로 포트를 열 때마다 증가하는 연결 세대입니다.</summary>
        long ConnectionGeneration { get; }
        /// <summary>이번 응답의 경계와 대기·송수신 전체 제한 시간을 지정합니다.</summary>
        Task<byte[]> ExchangeAsync(byte[] request, IFrameBoundary boundary, TimeSpan timeout,
            Func<byte[], ResponseDisposition> classifyResponse = null, CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>여러 송수신을 하나의 독점 구간에서 수행합니다. 콜백 내부에서 이 클라이언트를 다시 호출하지 마세요.</summary>
        Task<T> ExecuteTransactionAsync<T>(Func<ISerialTransaction, Task<T>> action,
            CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>독점 구간의 대기와 실행에 하나의 제한 시간을 적용합니다. 콜백 예외는 포트를 폐기합니다.</summary>
        Task<T> ExecuteTransactionAsync<T>(Func<ISerialTransaction, Task<T>> action, TimeSpan timeout,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
