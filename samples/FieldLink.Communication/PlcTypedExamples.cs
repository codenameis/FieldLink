using System.Threading;
using System.Threading.Tasks;
using FieldLink.PlcDrivers;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.Samples
{
    /// <summary>제조사 선택 이후 동일한 자료형 API로 시험 영역을 쓰고 읽는 예제입니다.</summary>
    public static class PlcTypedExamples
    {
        /// <summary>Siemens의 DB1.0부터 DINT 세 개를 기록하고 읽습니다.</summary>
        public static Task<OperationResult<int[]>> SiemensAsync(string ip, int port = 102, CancellationToken token = default(CancellationToken)) =>
            WriteAndReadAsync(Plc.SiemensS7Tcp(ip, port), "DB1.0", token);

        /// <summary>OMRON의 D100부터 int 세 개를 기록하고 읽습니다.</summary>
        public static Task<OperationResult<int[]>> OmronAsync(string ip, int port = 9600, CancellationToken token = default(CancellationToken)) =>
            WriteAndReadAsync(Plc.OmronFinsTcp(ip, port), "D100", token);

        /// <summary>LS의 D100부터 int 세 개를 기록하고 읽습니다.</summary>
        public static Task<OperationResult<int[]>> LsAsync(string ip, int port = 2004, CancellationToken token = default(CancellationToken)) =>
            WriteAndReadAsync(Plc.LsFastEnetTcp(ip, port), "D100", token);

        /// <summary>Logix의 Values DINT 배열 앞 세 원소를 기록하고 읽습니다.</summary>
        public static Task<OperationResult<int[]>> AllenBradleyAsync(string ip, int port = 44818, CancellationToken token = default(CancellationToken)) =>
            WriteAndReadAsync(Plc.AllenBradleyTcp(ip, port), "Values[0]", token);

        /// <summary>Modbus의 0번 레지스터부터 int 세 개를 기록하고 읽습니다.</summary>
        public static Task<OperationResult<int[]>> ModbusAsync(string ip, int port = 502, CancellationToken token = default(CancellationToken)) =>
            WriteAndReadAsync(Plc.ModbusTcp(ip, port), "0", token);

        private static async Task<OperationResult<int[]>> WriteAndReadAsync(PlcClient plc, string address, CancellationToken token)
        {
            using (plc)
            {
                var opened = await plc.OpenAsync(token).ConfigureAwait(false);
                if (!opened.IsSuccess)
                    return opened.ConvertFailed<int[]>();
                var written = await plc.WriteAsync(address, new[] { 10, 20, 30 }, token).ConfigureAwait(false);
                if (!written.IsSuccess)
                    return written.ConvertFailed<int[]>();
                var read = await plc.ReadInt32Async(address, 3, token).ConfigureAwait(false);
                var closed = await plc.CloseAsync(token).ConfigureAwait(false);
                return !read.IsSuccess || closed.IsSuccess ? read : closed.ConvertFailed<int[]>();
            }
        }
    }
}
