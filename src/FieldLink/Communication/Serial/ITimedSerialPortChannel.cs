using System;

namespace FieldLink.Communication.Serial
{
    /// <summary>문자별 물리 수신 간격을 제공할 수 있는 선택적 시리얼 어댑터입니다.</summary>
    /// <remarks>
    /// 기본 SystemSerialPortChannel은 이 계약을 구현하지 않습니다. 기존 Read와 같은 입력 스트림을 소비해야 합니다.
    /// 구현체는 자체 버퍼를 사용할 수 있으나 바이트와 시간 정보의 순서를 보존해야 합니다.
    /// </remarks>
    public interface ITimedSerialPortChannel : ISerialPortChannel
    {
        /// <summary>한 바이트를 소비하고 이전 문자와의 물리적 무수신 간격 하한을 반환합니다. 수신이 없으면 TimeoutException을 던집니다.</summary>
        /// <param name="timeoutMilliseconds">읽기 대기 상한입니다. Dispose 시에는 기존 Read처럼 대기를 중단해야 합니다.</param>
        /// <param name="minimumSilentInterval">
        /// 이전 문자 종료부터 현재 문자 시작까지 침묵의 검증 가능한 하한입니다. 시각 측정 오차를 빼야 하며 음수는 허용하지 않습니다.
        /// 알 수 없으면 null입니다. 이전 Read 호출을 가로지르는 간격도 포함하며 Read 반환 시각이나 DataReceived 이벤트 간격으로 대체하면 안 됩니다.
        /// </param>
        /// <returns>입력 스트림의 다음 바이트입니다.</returns>
        byte ReadByteWithTiming(int timeoutMilliseconds, out TimeSpan? minimumSilentInterval);
    }
}
