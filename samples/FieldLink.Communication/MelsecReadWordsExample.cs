using FieldLink.Communication;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec.Clients;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TcpClient = FieldLink.Communication.Tcp.TcpClient;

namespace FieldLink.Samples
{
    /// <summary>Melsec 클라이언트로 D100부터 50워드를 읽고 전송 자원을 정리하는 예제입니다.</summary>
    public static class MelsecReadWordsExample
    {
        /// <summary>호출자가 지정한 PLC 또는 가상 PLC에 한 번 접속하여 읽습니다.</summary>
        public static async Task<OperationResult<ushort[]>> ReadD100Async(IPEndPoint endPoint,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (ITcpClient transport = new TcpClient(endPoint, new MelsecMc3EBinaryFrame()))
            {
                var plc = new MelsecMc3EBinaryTcpClient(transport, timeout: TimeSpan.FromSeconds(3));
                await transport.OpenAsync(cancellationToken).ConfigureAwait(false);
                return await plc.ReadWordsAsync("D100", 50, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
