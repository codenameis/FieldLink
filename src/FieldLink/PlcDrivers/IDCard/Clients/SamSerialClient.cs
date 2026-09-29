using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.IDCard;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.IDCard.Clients
{
    /// <summary>SAM 모듈과 카드 판독 명령을 교환합니다. 포트는 호출자가 관리합니다.</summary>
    public sealed class SamSerialClient
    {
        private readonly SerialProtocolExchange exchange;
        /// <summary>전송과 응답 제한 시간을 지정합니다.</summary>
        public SamSerialClient(ISerialClient transport, TimeSpan? timeout = null) =>
            exchange = new SerialProtocolExchange(transport, new HeaderLengthFrame(7, header => 7 + header.Array[header.Offset + 5] * 256 + header.Array[header.Offset + 6]), timeout);
        /// <summary>보안 모듈 번호를 읽습니다.</summary>
        public Task<OperationResult<string>> ReadSafeModuleNumberAsync(CancellationToken token = default(CancellationToken)) => ExecuteAsync(0x12, 0xFF, SAMSerialResponseParser.ExtractSafeModuleNumber, token);
        /// <summary>보안 모듈 상태를 확인합니다.</summary>
        public Task<OperationResult> CheckSafeModuleStatusAsync(CancellationToken token = default(CancellationToken)) => CheckAsync(0x12, 0xFF, 0x90, token);
        /// <summary>판독 영역의 카드를 검색합니다.</summary>
        public Task<OperationResult> SearchCardAsync(CancellationToken token = default(CancellationToken)) => CheckAsync(0x20, 1, 0x9F, token);
        /// <summary>검색한 카드를 선택합니다.</summary>
        public Task<OperationResult> SelectCardAsync(CancellationToken token = default(CancellationToken)) => CheckAsync(0x20, 2, 0x90, token);
        /// <summary>선택한 카드의 신원 정보를 읽습니다.</summary>
        public Task<OperationResult<IdentityCard>> ReadCardAsync(CancellationToken token = default(CancellationToken)) => ExecuteAsync(0x30, 1, SAMSerialResponseParser.ExtractIdentityCard, token);
        private async Task<OperationResult> CheckAsync(byte command, byte parameter, byte expected, CancellationToken token) =>
            await ExecuteAsync(command, parameter, r => r[9] == expected ? OperationResult.CreateSuccessResult(true) : new OperationResult<bool>(SAMSerialResponseParser.GetErrorDescription(r[9])), token).ConfigureAwait(false);
        private Task<OperationResult<T>> ExecuteAsync<T>(byte command, byte parameter, Func<byte[], OperationResult<T>> parse, CancellationToken token) =>
            exchange.ExecuteAsync(OperationResult.CreateSuccessResult(SAMSerialCommandBuilder.PackToSAMCommand(SAMSerialCommandBuilder.BuildReadCommand(command, parameter, null))), response =>
            {
                var check = SAMSerialResponseParser.CheckADSCommandAndSum(response);
                if (!check.IsSuccess)
                    return check.ConvertFailed<T>();
                if (response.Length < 11)
                    return new OperationResult<T>("SAM 응답에 상태 코드가 없습니다.");
                return parse(response);
            }, token);
    }
}
