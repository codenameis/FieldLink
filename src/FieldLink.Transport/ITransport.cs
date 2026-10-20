using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Transport;

/// <summary>장치와 바이트를 주고받는 연결 하나를 나타냅니다. (TCP 소켓, UDP 소켓, 시리얼 포트)</summary>
/// <remarks>
/// <para>생성만으로는 연결하지 않습니다. <see cref="OpenAsync"/>를 완료한 뒤 사용합니다.</para>
/// <para>자동 재접속·재전송을 하지 않습니다. 실패 처리는 호출자가 결정합니다.</para>
/// <para>제한 시간은 <see cref="CancellationToken"/>으로 전달합니다. (예: <c>CancellationTokenSource.CancelAfter</c>)</para>
/// </remarks>
public interface ITransport : IDisposable
{
    /// <summary>조회 시점의 연결 상태입니다.</summary>
    TransportState State { get; }

    /// <summary>연결을 수립합니다.</summary>
    /// <param name="cancellationToken">취소 또는 제한 시간 토큰입니다.</param>
    /// <exception cref="TransportException">연결하지 못했을 때.</exception>
    /// <exception cref="InvalidOperationException">이미 열려 있거나 여는 중일 때.</exception>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>연결을 닫습니다. 진행 중인 교환은 <see cref="TransportFailure.ConnectionClosed"/>로 끝납니다.</summary>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>다른 호출이 끼어들 수 없는 독점 구간에서 하나 이상의 송수신을 실행합니다.</summary>
    /// <typeparam name="TResult">작업 결과 형식입니다.</typeparam>
    /// <param name="work">독점 구간에서 실행할 작업입니다. 세션은 이 작업 안에서만 유효합니다.</param>
    /// <param name="cancellationToken">대기·송수신 전체에 적용되는 취소 또는 제한 시간 토큰입니다.</param>
    /// <returns><paramref name="work"/>의 결과입니다.</returns>
    /// <remarks>세션 협상(예: FINS 노드 교환)이나 분할 전송처럼 순서가 보장되어야 하는 교환에 사용합니다.</remarks>
    Task<TResult> RunExclusiveAsync<TResult>(Func<ITransportSession, Task<TResult>> work, CancellationToken cancellationToken = default);
}
