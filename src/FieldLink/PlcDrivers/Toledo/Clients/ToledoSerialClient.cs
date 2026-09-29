using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Toledo;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Toledo.Clients
{
    /// <summary>Toledo 연속 출력 프레임을 전송 없이 수신합니다. 포트 설정에는 장치 요구에 따라 RTS를 지정하세요.</summary>
    public sealed class ToledoSerialClient
    {
        private readonly ISerialClient transport;
        private readonly IFrameBoundary boundary;
        private readonly TimeSpan timeout;
        /// <summary>체크 바이트 유무와 한 프레임의 대기 시간을 지정합니다. 생성만으로 수신을 시작하지 않습니다.</summary>
        public ToledoSerialClient(ISerialClient transport, bool hasCheckByte = false, TimeSpan? timeout = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            boundary = new ToledoBoundary(hasCheckByte);
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
        }
        /// <summary>다음 계량 데이터를 기다립니다. 반복 수신은 이 메서드를 순차 호출하며 취소 토큰으로 종료하세요.</summary>
        public Task<ToledoStandardData> ReceiveAsync(CancellationToken token = default(CancellationToken)) => transport.ExecuteTransactionAsync(async tx =>
            new ToledoStandardData(await tx.ReceiveAsync(boundary).ConfigureAwait(false)), timeout, token);
        private sealed class ToledoBoundary : IFrameBoundary
        {
            private readonly bool check;
            internal ToledoBoundary(bool check) { this.check = check; }
            public int? GetFrameLength(ArraySegment<byte> data)
            {
                for (int i = 15; i < data.Count; i++)
                    if (data.Array[data.Offset + i] == 13)
                        return i + 1 + (check ? 1 : 0);
                return null;
            }
        }
    }
}
