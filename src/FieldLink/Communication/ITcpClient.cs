using FieldLink.Communication.Tcp;
using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication
{
    /// <summary>TCP 연결과 요청·응답, 여러 단계의 독점 교환을 제공하는 클라이언트 계약입니다.</summary>
    public interface ITcpClient : ICommunicationClient
    {
        /// <summary>로컬 연결 상태입니다. 원격 장치의 생존을 보장하지 않습니다.</summary>
        bool IsConnected { get; }
        /// <summary>현재 TCP 연결의 세대 번호입니다. 최초 연결 전에는 0이며 연결 성공마다 증가합니다.</summary>
        /// <remarks>재연결 전후를 구분하여 이전 장치 세션·초기화 결과의 재사용을 판단합니다. 장치 준비 완료를 의미하지 않습니다.</remarks>
        long ConnectionGeneration { get; }
        /// <summary>이번 응답의 경계 규칙과 전체 제한 시간을 지정합니다.</summary>
        /// <remarks>Open 완료 후 호출하세요. 요청은 복사하지 않으며 반환 Task가 끝날 때까지 호출자가 배열을 유지해야 합니다.</remarks>
        Task<byte[]> ExchangeAsync(byte[] request, IFrameBoundary boundary, TimeSpan timeout,
            Func<byte[], ResponseDisposition> classifyResponse = null,
            CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>여러 교환을 독점 실행합니다. 콜백 안에서 각 교환을 순서대로 await하세요.</summary>
        /// <remarks>Open 상태가 아니면 콜백을 실행하지 않고 실패합니다. 콜백 예외는 연결을 폐기합니다. 연결을 유지할 정상 장치 오류는 결과로 반환하고 이 작업 완료 후 해석하세요.</remarks>
        Task<T> ExecuteTransactionAsync<T>(Func<ITcpTransaction, Task<T>> action,
            CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>독점 교환 전체에 하나의 제한 시간을 적용합니다.</summary>
        /// <remarks>Open 상태가 아니면 대기 없이 실패합니다. 장치 명령 거절과 통신 실패를 구분하세요. 정상 거절 응답을 콜백 예외로 전달하면 소켓도 종료됩니다.</remarks>
        Task<T> ExecuteTransactionAsync<T>(Func<ITcpTransaction, Task<T>> action, TimeSpan timeout,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
