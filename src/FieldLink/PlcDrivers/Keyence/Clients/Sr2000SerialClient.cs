using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Keyence;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Keyence.Clients
{
    /// <summary>SR2000의 CR 종료 명령을 교환합니다. 전송은 호출자가 열고 닫습니다.</summary>
    public sealed class Sr2000SerialClient
    {
        private readonly SerialProtocolExchange exchange;
        /// <summary>전송과 응답 제한 시간(기본 10초)을 지정합니다.</summary>
        public Sr2000SerialClient(ISerialClient transport, TimeSpan? timeout = null) => exchange = new SerialProtocolExchange(transport, new DelimitedFrame(new byte[] { 13 }), timeout ?? TimeSpan.FromSeconds(10));
        /// <summary>사용자 정의 명령을 보내고 장치 오류 또는 결과 문자열을 반환합니다.</summary>
        public Task<OperationResult<string>> ReadCustomAsync(string command, CancellationToken token = default(CancellationToken)) => exchange.ExecuteAsync(OperationResult.CreateSuccessResult(Sr2000CommandBuilder.BuildCustom(command)), r => Sr2000ResponseParser.Parse(command, r), token);
        /// <summary>바코드를 읽습니다.</summary>
        public Task<OperationResult<string>> ReadBarcodeAsync(CancellationToken token = default(CancellationToken)) => ReadCustomAsync("LON", token);
        /// <summary>장치를 재설정합니다.</summary>
        public async Task<OperationResult> ResetAsync(CancellationToken token = default(CancellationToken)) => await ReadCustomAsync("RESET", token).ConfigureAwait(false);
        /// <summary>표시등을 켭니다.</summary>
        public async Task<OperationResult> OpenIndicatorAsync(CancellationToken token = default(CancellationToken)) => await ReadCustomAsync("AMON", token).ConfigureAwait(false);
        /// <summary>표시등을 끕니다.</summary>
        public async Task<OperationResult> CloseIndicatorAsync(CancellationToken token = default(CancellationToken)) => await ReadCustomAsync("AMOFF", token).ConfigureAwait(false);
        /// <summary>펌웨어 버전을 읽습니다.</summary>
        public Task<OperationResult<string>> ReadVersionAsync(CancellationToken token = default(CancellationToken)) => ReadCustomAsync("KEYENCE", token);
        /// <summary>명령 상태를 읽습니다.</summary>
        public Task<OperationResult<string>> ReadCommandStateAsync(CancellationToken token = default(CancellationToken)) => ReadCustomAsync("CMDSTAT", token);
        /// <summary>오류 상태를 읽습니다.</summary>
        public Task<OperationResult<string>> ReadErrorStateAsync(CancellationToken token = default(CancellationToken)) => ReadCustomAsync("ERRSTAT", token);
        /// <summary>지정 입력 상태를 읽습니다.</summary>
        public async Task<OperationResult<bool>> CheckInputAsync(int number, CancellationToken token = default(CancellationToken))
        {
            var result = await ReadCustomAsync("INCHK," + number, token).ConfigureAwait(false);
            return result.IsSuccess ? Sr2000ResponseParser.ParseInput(result.Content) : result.ConvertFailed<bool>();
        }
        /// <summary>지정 출력을 설정합니다.</summary>
        public async Task<OperationResult> SetOutputAsync(int number, bool value, CancellationToken token = default(CancellationToken)) => await ReadCustomAsync((value ? "OUTON," : "OUTOFF,") + number, token).ConfigureAwait(false);
        /// <summary>판독 통계 카운터를 읽습니다.</summary>
        public async Task<OperationResult<int[]>> ReadRecordAsync(CancellationToken token = default(CancellationToken))
        {
            var result = await ReadCustomAsync("NUM", token).ConfigureAwait(false);
            return result.IsSuccess ? OperationResult.CreateSuccessResult(Sr2000ResponseParser.ParseRecord(result.Content)) : result.ConvertFailed<int[]>();
        }
        /// <summary>키 조작을 잠급니다.</summary>
        public async Task<OperationResult> LockAsync(CancellationToken token = default(CancellationToken)) => await ReadCustomAsync("LOCK", token).ConfigureAwait(false);
        /// <summary>키 조작 잠금을 해제합니다.</summary>
        public async Task<OperationResult> UnlockAsync(CancellationToken token = default(CancellationToken)) => await ReadCustomAsync("UNLOCK", token).ConfigureAwait(false);
    }
}
