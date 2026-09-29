using FieldLink.PlcDrivers;
using FieldLink.PlcDrivers.Common;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Samples
{
    /// <summary>시험용 D100~D105에 int 세 개를 쓰고 읽습니다. PLC 설정에 맞는 IP·포트를 지정하세요.</summary>
    public static class MelsecTypedExample
    {
        /// <summary>연결·연속 쓰기·읽기·해제를 하나의 클라이언트로 수행합니다.</summary>
        public static async Task<OperationResult<int[]>> WriteAndReadAsync(string ipAddress, int port,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var plc = Plc.MelsecMcTcp(ipAddress, port))
            {
                var opened = await plc.OpenAsync(cancellationToken).ConfigureAwait(false);
                if (!opened.IsSuccess)
                    return opened.ConvertFailed<int[]>();

                var written = await plc.WriteAsync("D100", new[] { 10, 20, 30 }, cancellationToken).ConfigureAwait(false);
                if (!written.IsSuccess)
                    return written.ConvertFailed<int[]>();

                return await plc.ReadInt32Async("D100", 3, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
