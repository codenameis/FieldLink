using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers.LSIS.Clients
{
    internal sealed class LsFastEnetDriver : IPlcDriver
    {
        internal static readonly IFrameBoundary Frame = new HeaderLengthFrame(20, h =>
            20 + h.Array[h.Offset + 16] + h.Array[h.Offset + 17] * 256);
        private readonly ITcpClient transport;
        private readonly FastEnetFrameOptions options;
        private readonly int limit;
        private ushort sequence;

        internal LsFastEnetDriver(ITcpClient transport, LsFastEnetClientOptions settings)
        {
            if (settings.Base > 15 || settings.Slot > 15 || settings.MaxBytesPerRequest < 1 || settings.MaxBytesPerRequest > 1400 ||
                !Enum.IsDefined(typeof(LSCpuInfo), settings.Cpu))
                throw new ArgumentOutOfRangeException(nameof(settings));
            this.transport = transport;
            options = new FastEnetFrameOptions { cpuInfo = settings.Cpu, baseNo = settings.Base, slotNo = settings.Slot };
            limit = settings.MaxBytesPerRequest;
        }
        public int StringAlignment => 1;
        public bool HasStringLength => false;
        public Task InitializeAsync(TimeSpan timeout, CancellationToken token) => Task.CompletedTask;
        public Task ShutdownAsync(TimeSpan timeout, CancellationToken token) => Task.CompletedTask;

        public Task<OperationResult<byte[]>> TransferAsync(PlcTransferRequest request, TimeSpan timeout, CancellationToken token)
        {
            string address = request.Address.TrimStart('%').ToUpperInvariant();
            if (address.Length < 2 || "PMLKFTCDSNZR".IndexOf(address[0]) < 0)
                throw new PlcProtocolException(-1, "간편 LS 주소는 D100, DB200 또는 MX2764처럼 메모리 위치를 지정하세요.");
            bool explicitType = address[1] == 'B' || address[1] == 'X';
            if (request.IsBit != (address[1] == 'X'))
                throw new PlcProtocolException(-1, "비트에는 X와 10진수 비트 번호를, 숫자에는 워드/바이트 주소를 사용하세요.");
            if (!int.TryParse(address.Substring(explicitType ? 2 : 1), NumberStyles.None, CultureInfo.InvariantCulture, out int start))
                throw new PlcProtocolException(-1, "LS 메모리 주소는 음수가 아닌 10진수여야 합니다.");
            long position = explicitType ? start : start * 2L;
            if (position + request.ByteCount > int.MaxValue)
                throw new PlcProtocolException(-1, "LS 메모리 주소 범위를 벗어납니다.");
            start = (int)position;
            request.Unit = request.IsBit ? "bit" : "byte";
            byte[] payload = request.Encode();
            return transport.ExecuteTransactionAsync(async tx =>
            {
                var output = request.IsWrite ? new byte[0] : new byte[request.ByteCount];
                while (request.Confirmed < request.ByteCount)
                {
                    int block = Math.Min(request.IsBit ? 1 : limit, request.ByteCount - request.Confirmed);
                    string location = address[0] + (request.IsBit ? "X" : "B") + (start + request.Confirmed).ToString(CultureInfo.InvariantCulture);
                    byte[] core;
                    if (request.IsWrite)
                    {
                        var data = new byte[block];
                        Array.Copy(payload, request.Confirmed, data, 0, block);
                        core = PlcTransferRequest.Require(LSFastEnetCommandBuilder.BuildWriteByteCommand(options, location, data));
                    }
                    else
                        core = PlcTransferRequest.Require(request.IsBit
                            ? LSFastEnetCommandBuilder.BuildReadIndividualCommand(0, location)
                            : LSFastEnetCommandBuilder.BuildReadByteCommand(location, (ushort)block));
                    byte[] command = LSFastEnetCommandBuilder.PackCommandWithHeader(options, core);
                    ushort id = unchecked(++sequence);
                    command[14] = (byte)id;
                    command[15] = (byte)(id >> 8);
                    int sum = 0;
                    for (int i = 0; i < 19; i++) sum += command[i];
                    command[19] = (byte)sum;
                    request.Pending = block;
                    OperationResult<byte[]> result = null;
                    await tx.ExchangeAsync(command, Frame, response =>
                    {
                        if (response.Length < 29 || response.Length != 20 + response[16] + response[17] * 256 || response[13] != 0x11)
                            return ResponseDisposition.Reject;
                        for (int i = 0; i < 10; i++)
                            if (response[i] != command[i]) return ResponseDisposition.Reject;
                        if (response[14] != command[14] || response[15] != command[15])
                            return ResponseDisposition.Ignore;
                        if (response[20] != (request.IsWrite ? 0x59 : 0x55) || response[21] != 0 || response[22] != core[2] || response[23] != 0)
                            return ResponseDisposition.Reject;
                        bool error = response[26] != 0 || response[27] != 0;
                        if (error)
                        {
                            if (response.Length != 29 && response.Length != 30)
                                return ResponseDisposition.Reject;
                        }
                        else if (response.Length < 30 || response[28] != 1 || response[29] != 0 ||
                            (request.IsWrite ? response.Length != 30 : response.Length != 32 + block || response[30] + response[31] * 256 != block))
                            return ResponseDisposition.Reject;
                        result = LSFastEnetResponseParser.ExtractActualData(options, response);
                        if (result.IsSuccess && request.IsBit && !request.IsWrite && result.Content[0] > 1)
                            return ResponseDisposition.Reject;
                        return ResponseDisposition.Accept;
                    }).ConfigureAwait(false);
                    if (!result.IsSuccess)
                    {
                        request.Pending = 0;
                        return request.Failed(result);
                    }
                    if (!request.IsWrite)
                        Array.Copy(result.Content, 0, output, request.Confirmed, block);
                    request.Confirm();
                }
                return OperationResult.CreateSuccessResult(output);
            }, timeout, token);
        }
    }
}
