using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Plc.Abstractions;

/// <summary>제조사 프로토콜 하나의 실행 규칙입니다. (Strategy)</summary>
/// <remarks>
/// <para>
/// 구현체는 생성자로 <c>ITransport</c>와 옵션을 받습니다. 주소 해석 → 요청 분할 → 요청 프레임 생성 →
/// 송수신 → 응답 해석의 순서를 조율하며, 각 단계는 Address·RequestBuilder·ResponseParser에 맡깁니다.
/// </para>
/// <para>전송의 열기·닫기·해제는 이 인터페이스의 책임이 아닙니다. 전송을 소유한 쪽(PlcClient)이 합니다.</para>
/// <para>
/// 실패 알림 규칙:
/// 통신 실패는 <c>TransportException</c>을 그대로 전달하고, 장치 거절은 <see cref="PlcDeviceException"/>,
/// 손상된 응답은 <see cref="PlcErrorKind.InvalidResponse"/>인 <see cref="PlcException"/>,
/// 잘못된 주소·인수는 <see cref="ArgumentException"/>으로 알립니다.
/// </para>
/// </remarks>
public interface IPlcProtocol
{
    /// <summary>값 변환에 필요한 프로토콜 고정 특성입니다.</summary>
    PlcProtocolTraits Traits { get; }

    /// <summary>전송이 열린 직후 프로토콜 초기 협상을 수행합니다. 협상이 없는 프로토콜은 즉시 완료합니다.</summary>
    /// <param name="cancellationToken">취소 또는 제한 시간 토큰입니다.</param>
    /// <remarks>예: OMRON FINS/TCP 노드 주소 교환, EtherNet/IP RegisterSession, S7 COTP·Setup Communication.</remarks>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>전송을 닫기 전에 프로토콜 정상 해제 절차를 수행합니다. 절차가 없으면 즉시 완료합니다.</summary>
    /// <param name="cancellationToken">취소 또는 제한 시간 토큰입니다.</param>
    /// <remarks>예: EtherNet/IP UnRegisterSession.</remarks>
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>연속된 원소를 읽습니다. 장치 한도를 넘으면 내부에서 나눠 요청합니다.</summary>
    /// <param name="request">읽기 요청입니다.</param>
    /// <param name="cancellationToken">요청 전체(모든 분할 요청 포함)에 적용되는 취소 또는 제한 시간 토큰입니다.</param>
    /// <returns>
    /// 길이가 정확히 <see cref="PlcReadRequest.ByteCount"/>인 바이트입니다. 숫자는 PLC 바이트 순서 그대로입니다.
    /// </returns>
    Task<byte[]> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken);

    /// <summary>연속된 원소를 쓰고 장치 응답으로 완료를 확인합니다. 장치 한도를 넘으면 내부에서 나눠 요청합니다.</summary>
    /// <param name="request">쓰기 요청입니다.</param>
    /// <param name="cancellationToken">요청 전체에 적용되는 취소 또는 제한 시간 토큰입니다.</param>
    /// <remarks>
    /// 분할 쓰기는 원자적이지 않습니다. 중간에 실패하면 완료가 확인된 원소 수를
    /// <see cref="PlcException.CompletedCount"/>에 담아 던집니다.
    /// </remarks>
    Task WriteAsync(PlcWriteRequest request, CancellationToken cancellationToken);
}
