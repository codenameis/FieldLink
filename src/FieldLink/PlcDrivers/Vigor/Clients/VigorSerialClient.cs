using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Vigor;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Vigor.Clients
{
    /// <summary>Vigor VS의 DLE 이스케이프 프레임을 교환합니다.</summary>
    public sealed class VigorSerialClient
    {
        private readonly SerialProtocolExchange exchange;
        /// <summary>호출자가 소유한 포트와 기본 국번을 지정합니다.</summary>
        public VigorSerialClient(ISerialClient transport, byte station = 0, TimeSpan? timeout = null)
        { Station = station; exchange = new SerialProtocolExchange(transport, new EscapedTerminatorFrame(0x10, 3, 2), timeout); }
        /// <summary>주소에서 재정의하지 않은 기본 국번입니다.</summary>
        public byte Station { get; }
        /// <summary>워드 영역을 읽습니다. C200 이후 카운터 길이 단위는 32비트입니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return exchange.ExecuteSequenceAsync(VigorVsCommandBuilder.BuildReadCommand(station, address, length, false),
                (i, response) => VigorVsResponseParser.CheckResponseContent(response), cancellationToken);
        }
        /// <summary>비트 영역을 분할하여 읽습니다.</summary>
        public Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return exchange.ExecuteSequenceAsync(VigorVsCommandBuilder.BuildReadCommand(station, address, length, true),
                (i, response) =>
                {
                    var read = VigorVsResponseParser.CheckResponseContent(response);
                    if (!read.IsSuccess)
                        return read.ConvertFailed<bool[]>();
                    // 이전 구현: read.Content.ToBoolArray().SelectBegin(length)
                    // R-019: 전체 길이를 각 구간에 적용하면 마지막 바이트의 패딩 비트가 결과에 섞인다.
                    int segmentLength = Math.Min(1024, length - i * 1024);
                    if (read.Content.Length != (segmentLength + 7) / 8)
                        return new OperationResult<bool[]>("Vigor 비트 응답 길이가 요청 구간과 일치하지 않습니다.");
                    return OperationResult.CreateSuccessResult(read.Content.ToBoolArray().SelectBegin(segmentLength));
                }, cancellationToken);
        }
        /// <summary>워드 영역에 데이터를 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return await exchange.ExecuteAsync(VigorVsCommandBuilder.BuildWriteWordCommand(station, address, value),
                VigorVsResponseParser.CheckResponseContent, cancellationToken).ConfigureAwait(false);
        }
        /// <summary>비트 영역에 데이터를 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, bool[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte station = StationFor(ref address);
            return await exchange.ExecuteAsync(VigorVsCommandBuilder.BuildWriteBoolCommand(station, address, value),
                VigorVsResponseParser.CheckResponseContent, cancellationToken).ConfigureAwait(false);
        }
        private byte StationFor(ref string address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            return (byte)AddressParameters.ExtractParameter(ref address, "s", Station);
        }
    }
}
