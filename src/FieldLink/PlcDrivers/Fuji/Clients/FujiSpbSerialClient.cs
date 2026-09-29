using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Fuji;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Fuji.Clients
{
    /// <summary>Fuji SPB의 CR·LF 종료 프레임을 교환합니다.</summary>
    public sealed class FujiSpbSerialClient
    {
        private readonly SerialProtocolExchange exchange;
        /// <summary>호출자가 소유한 포트와 기본 국번을 지정합니다.</summary>
        public FujiSpbSerialClient(ISerialClient transport, byte station = 1, TimeSpan? timeout = null)
        { Station = station; exchange = new SerialProtocolExchange(transport, new DelimitedFrame(new byte[] { 13, 10 }), timeout); }
        /// <summary>주소에서 재정의하지 않은 기본 국번입니다.</summary>
        public byte Station { get; }
        /// <summary>워드 데이터를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken)) =>
            exchange.ExecuteAsync(FujiSPBCommandBuilder.BuildReadCommand(Station, address, length), FujiSpbReadResponseParser.ParseWords, cancellationToken);
        /// <summary>코일 또는 레지스터의 점 주소부터 연속 비트를 읽습니다.</summary>
        public async Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
            var analysis = FujiSpbAddress.ParseFrom(address);
            if (!analysis.IsSuccess)
                return analysis.ConvertFailed<bool[]>();
            if ((address.StartsWith("X") || address.StartsWith("Y") || address.StartsWith("M") || address.StartsWith("L") ||
                address.StartsWith("TC") || address.StartsWith("CC")) && address.IndexOf('.') < 0)
            {
                analysis.Content.BitIndex = analysis.Content.AddressStart % 16;
                analysis.Content.AddressStart = (ushort)(analysis.Content.AddressStart / 16);
            }
            ushort words = (ushort)((analysis.Content.GetBitIndex() + length - 1) / 16 - analysis.Content.GetBitIndex() / 16 + 1);
            var read = await exchange.ExecuteAsync(FujiSPBCommandBuilder.BuildReadCommand(station, analysis.Content, words),
                FujiSpbReadResponseParser.ParseWords, cancellationToken).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(read.Content.ToBoolArray().SelectMiddle(analysis.Content.BitIndex, length)) : read.ConvertFailed<bool[]>();
        }
        /// <summary>워드 데이터를 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken)) =>
            await exchange.ExecuteAsync(FujiSPBCommandBuilder.BuildWriteByteCommand(Station, address, value), FujiSPBResponseParser.CheckResponseData, cancellationToken).ConfigureAwait(false);
        /// <summary>비트 하나를 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, bool value, CancellationToken cancellationToken = default(CancellationToken)) =>
            await exchange.ExecuteAsync(FujiSPBCommandBuilder.BuildWriteBoolCommand(Station, address, value), FujiSPBResponseParser.CheckResponseData, cancellationToken).ConfigureAwait(false);
    }
}
