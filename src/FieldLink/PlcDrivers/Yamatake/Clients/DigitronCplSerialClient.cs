using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Yamatake;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Yamatake.Clients
{
    /// <summary>Digitron CPL의 CR·LF 종료 프레임을 교환합니다.</summary>
    public sealed class DigitronCplSerialClient
    {
        private readonly SerialProtocolExchange exchange;
        /// <summary>호출자가 소유한 시리얼 포트와 기본 국번을 지정합니다.</summary>
        public DigitronCplSerialClient(ISerialClient transport, byte station = 1, TimeSpan? timeout = null)
        { Station = station; exchange = new SerialProtocolExchange(transport, new DelimitedFrame(new byte[] { 13, 10 }), timeout); }
        /// <summary>주소에서 재정의하지 않은 기본 국번입니다.</summary>
        public byte Station { get; }
        /// <summary>워드 데이터를 읽고 CPL 상태와 데이터를 해석합니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
            return exchange.ExecuteAsync(DigitronCPLCommandBuilder.BuildReadCommand(station, address, length), DigitronCPLResponseParser.ExtraActualResponse, cancellationToken);
        }
        /// <summary>워드 데이터를 기록하고 CPL 승인 상태를 확인합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            byte station = (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
            return await exchange.ExecuteAsync(DigitronCPLCommandBuilder.BuildWriteCommand(station, address, value), DigitronCPLResponseParser.ExtraActualResponse, cancellationToken).ConfigureAwait(false);
        }
    }
}
