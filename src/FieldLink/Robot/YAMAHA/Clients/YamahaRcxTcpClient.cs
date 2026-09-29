using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.YAMAHA.Protocols;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.YAMAHA.Clients
{
    /// <summary>RCX 명령을 한 번 전송하고 해당 명령의 응답 행들을 순서대로 수신합니다.</summary>
    public sealed class YamahaRcxTcpClient
    {
        private readonly ITcpClient transport;
        private readonly TimeSpan timeout;
        private readonly IFrameBoundary boundary = new DelimitedFrame(new byte[] { 13, 10 });
        /// <summary>호출자가 관리하는 TCP 전송을 지정합니다.</summary>
        public YamahaRcxTcpClient(ITcpClient transport, TimeSpan? timeout = null)
        { this.transport = transport ?? throw new ArgumentNullException(nameof(transport)); this.timeout = timeout ?? TimeSpan.FromSeconds(3); ClientOperation.ValidateTimeout(this.timeout); }
        /// <summary>지정한 명령으로 여러 행을 읽습니다. 한 요청의 행들이 다른 요청과 섞이지 않습니다.</summary>
        public Task<OperationResult<string[]>> ReadCommandAsync(string command, int lines, CancellationToken cancellationToken = default(CancellationToken)) => ReadFromServerAsync(YamahaRcxProtocol.BuildCommand(command), lines, cancellationToken);
        /// <summary>미리 구성한 명령 바이트로 응답 행을 읽습니다.</summary>
        public Task<OperationResult<string[]>> ReadFromServerAsync(byte[] command, int lines, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (lines < 1)
                throw new ArgumentOutOfRangeException(nameof(lines));
            return transport.ExecuteTransactionAsync(async tx =>
            {
                var result = new string[lines];
                result[0] = YamahaRcxProtocol.ParseLine(await tx.ExchangeAsync(command, boundary).ConfigureAwait(false));
                for (int i = 1; i < lines; i++)
                    result[i] = YamahaRcxProtocol.ParseLine(await tx.ReceiveAsync(boundary).ConfigureAwait(false));
                return OperationResult.CreateSuccessResult(result);
            }, timeout, cancellationToken);
        }
        private async Task<OperationResult> CommandAsync(byte[] command, CancellationToken token) => YamahaRcxProtocol.CheckResponseOk((await ReadFromServerAsync(command, 1, token).ConfigureAwait(false)).Content[0]);
        private async Task<OperationResult<int>> StatusAsync(byte[] command, CancellationToken token) => YamahaRcxProtocol.ParseStatus((await ReadFromServerAsync(command, 2, token).ConfigureAwait(false)).Content);
        /// <summary>초기화 명령을 실행합니다.</summary>
        public Task<OperationResult> ResetAsync(CancellationToken cancellationToken = default(CancellationToken)) => CommandAsync(YamahaRcxProtocol.BuildReset(), cancellationToken);
        /// <summary>프로그램을 실행합니다.</summary>
        public Task<OperationResult> RunAsync(CancellationToken cancellationToken = default(CancellationToken)) => CommandAsync(YamahaRcxProtocol.BuildRun(), cancellationToken);
        /// <summary>프로그램을 정지합니다.</summary>
        public Task<OperationResult> StopAsync(CancellationToken cancellationToken = default(CancellationToken)) => CommandAsync(YamahaRcxProtocol.BuildStop(), cancellationToken);
        /// <summary>프로그램을 지정 작업에 적재합니다.</summary>
        public Task<OperationResult> LoadAsync(string program, int taskId, CancellationToken cancellationToken = default(CancellationToken)) => CommandAsync(YamahaRcxProtocol.BuildLoad(program, taskId), cancellationToken);
        /// <summary>JOGXY 이동 수명주기를 지원하지 않으므로 명령 전송 없이 실패 결과를 반환합니다.</summary>
        public Task<OperationResult> JogXYAsync(int axis, int robot = 1, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new OperationResult("JOGXY는 200ms 간격의 실행 계속 신호와 RUN/END 처리 지원이 필요합니다. 현재 API는 이를 지원하지 않아 이동 명령을 전송하지 않습니다."));
        }
        /// <summary>모터 상태를 읽습니다.</summary>
        public Task<OperationResult<int>> ReadMotorStatusAsync(CancellationToken cancellationToken = default(CancellationToken)) => StatusAsync(YamahaRcxProtocol.BuildReadMotorStatus(), cancellationToken);
        /// <summary>운전 모드를 읽습니다.</summary>
        public Task<OperationResult<int>> ReadModeStatusAsync(CancellationToken cancellationToken = default(CancellationToken)) => StatusAsync(YamahaRcxProtocol.BuildReadModeStatus(), cancellationToken);
        /// <summary>비상 정지 상태를 읽습니다.</summary>
        public Task<OperationResult<int>> ReadEmergencyStatusAsync(CancellationToken cancellationToken = default(CancellationToken)) => StatusAsync(YamahaRcxProtocol.BuildReadEmergencyStatus(), cancellationToken);
        /// <summary>관절 좌표를 읽습니다.</summary>
        public async Task<OperationResult<float[]>> ReadJointsAsync(CancellationToken cancellationToken = default(CancellationToken)) => OperationResult.CreateSuccessResult(YamahaRcxProtocol.ParseJoints((await ReadFromServerAsync(YamahaRcxProtocol.BuildReadJoints(), 1, cancellationToken).ConfigureAwait(false)).Content[0]));
    }
}
