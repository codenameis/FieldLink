using FieldLink.Communication.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication
{
    /// <summary>한 장치와 메시지를 교환하는 클라이언트 계약입니다. 장치 명령과 프로토콜 세션은 드라이버가 관리합니다.</summary>
    public interface ICommunicationClient : IDisposable
    {
        /// <summary>로컬 전송 상태입니다. Open은 장치의 응답이나 초기화 완료를 보장하지 않습니다.</summary>
        ClientState State { get; }

        /// <summary>마지막 상태 변경과 원인입니다. 늦게 구독한 호출자도 현재 진단 정보를 조회할 수 있습니다.</summary>
        ClientStateChangedEventArgs LastStateChange { get; }

        /// <summary>상태 알림 구독자가 던진 마지막 예외입니다. 통신 오류와 별도로 보존합니다.</summary>
        Exception LastNotificationError { get; }

        /// <summary>상태 변경 순서대로 ThreadPool에서 전달합니다. UI 갱신은 UI 스레드로 전달하세요.</summary>
        event EventHandler<ClientStateChangedEventArgs> StateChanged;

        /// <summary>TCP 연결 또는 UDP 로컬 소켓을 명시적으로 엽니다. 열린 소켓은 종료 또는 활성 통신 실패까지 유지합니다.</summary>
        Task OpenAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>지정한 제한 시간으로 엽니다. 이미 열려 있거나 여는 중이면 대기 없이 실패하며, 통신 중 자동으로 다시 열지 않습니다.</summary>
        Task OpenAsync(TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>현재 소켓과 이전에 시작한 작업·연결 대기를 즉시 종료합니다. 장치 종료 메시지는 드라이버가 먼저 처리하세요.</summary>
        /// <remarks>다시 사용하려면 OpenAsync를 호출합니다. 재접속 여부·시점·횟수는 애플리케이션이 결정합니다.</remarks>
        void Close();

        /// <summary>메시지를 전송합니다. 장치의 처리 완료를 의미하지 않습니다.</summary>
        /// <remarks>Open 완료 후 호출하세요. 입력은 복사하지 않으므로 반환 Task가 끝날 때까지 배열을 수정·재사용·풀 반환하지 마세요.</remarks>
        Task SendAsync(byte[] message, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>열린 소켓에서 지정한 제한 시간으로 메시지를 전송합니다. 자동 재접속·재전송은 하지 않습니다.</summary>
        /// <remarks>Open 상태가 아니면 대기 없이 실패합니다. 입력은 복사하지 않으며 성공·실패·취소로 Task가 끝날 때까지 호출자가 유지합니다.</remarks>
        Task SendAsync(byte[] message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>메시지를 전송하고 응답 하나를 반환합니다. 식별자가 있는 프로토콜은 응답 판정기를 제공하세요.</summary>
        /// <remarks>Open 완료 후 호출하세요. 요청은 복사하지 않으므로 반환 Task가 끝날 때까지 유지하세요. 반환 응답은 독립 배열입니다.</remarks>
        Task<byte[]> ExchangeAsync(byte[] request, Func<byte[], ResponseDisposition> classifyResponse = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>대기·송신·수신에 하나의 제한 시간을 적용합니다. TCP는 기본 프레임 규칙, UDP는 데이터그램 경계를 사용합니다.</summary>
        /// <remarks>Open 상태가 아니면 대기 없이 실패합니다. 요청 배열은 성공·실패·취소로 Task가 끝날 때까지 수정·재사용·풀 반환하지 마세요.</remarks>
        Task<byte[]> ExchangeAsync(byte[] request, TimeSpan timeout,
            Func<byte[], ResponseDisposition> classifyResponse = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary>프로토콜이 수신 메시지를 현재 요청에 대응시키는 방법입니다.</summary>
    public enum ResponseDisposition
    {
        /// <summary>현재 요청의 응답입니다. 정상 형식의 장치 명령 거절도 포함하며, 명령 성공을 뜻하지 않습니다.</summary>
        Accept,
        /// <summary>다른 요청의 응답입니다. 같은 마감 시간 안에서 다음 메시지를 기다립니다.</summary>
        Ignore,
        /// <summary>유효하지 않은 응답입니다. 작업을 실패로 종료하고 소켓을 폐기합니다. 정상 장치 오류 코드에 사용하지 마세요.</summary>
        Reject
    }
}
