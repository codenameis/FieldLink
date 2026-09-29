using FieldLink.PlcDrivers.Modbus.Clients;
using FieldLink.PlcDrivers.Modbus;
using FieldLink.Communication;
using FieldLink.Communication.Internal;
using FieldLink.Communication.Tcp;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.Estun.Protocols;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.Estun.Clients
{
    /// <summary>Estun의 Modbus TCP 레지스터와 제어 순서를 실행합니다. 한 제어 작업 전체를 독점 교환으로 보호합니다.</summary>
    public sealed class EstunTcpClient
    {
        private readonly ITcpClient transport;
        private readonly byte station;
        private readonly TimeSpan timeout;
        private ushort transactionId;
        /// <summary>호출자가 관리하는 전송과 국번을 지정합니다.</summary>
        public EstunTcpClient(ITcpClient transport, byte station = 1, TimeSpan? timeout = null)
        { this.transport = transport ?? throw new ArgumentNullException(nameof(transport)); this.station = station; this.timeout = timeout ?? TimeSpan.FromSeconds(5); ClientOperation.ValidateTimeout(this.timeout); }
        /// <summary>주소 0의 100워드 상태를 읽습니다.</summary>
        public Task<OperationResult<EstunControllerState>> ReadDataAsync(CancellationToken cancellationToken = default(CancellationToken)) => transport.ExecuteTransactionAsync(async tx =>
        {
            var read = await ReadAsync(tx, 0, 100).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(EstunProtocol.ParseData(read.Content)) : OperationResult.CreateFailedResult<EstunControllerState>(read);
        }, timeout, cancellationToken);
        /// <summary>프로그램을 시작합니다.</summary>
        public Task<OperationResult> StartProgramAsync(CancellationToken cancellationToken = default(CancellationToken)) => ExecuteAsync(EstunProtocol.BuildCommand(EstunProtocol.StartProgram), cancellationToken);
        /// <summary>프로그램을 정지합니다.</summary>
        public Task<OperationResult> StopProgramAsync(CancellationToken cancellationToken = default(CancellationToken)) => ExecuteAsync(EstunProtocol.BuildCommand(EstunProtocol.StopProgram), cancellationToken);
        /// <summary>로봇 오류를 초기화합니다.</summary>
        public Task<OperationResult> ResetErrorAsync(CancellationToken cancellationToken = default(CancellationToken)) => ExecuteAsync(EstunProtocol.BuildCommand(EstunProtocol.ResetError), cancellationToken);
        /// <summary>프로젝트 이름을 기록하고 적재합니다.</summary>
        public Task<OperationResult> LoadProjectAsync(string name, CancellationToken cancellationToken = default(CancellationToken)) => ExecuteAsync(EstunProtocol.BuildLoadProject(name), cancellationToken);
        /// <summary>프로젝트 등록을 해제합니다.</summary>
        public Task<OperationResult> UnregisterProjectAsync(CancellationToken cancellationToken = default(CancellationToken)) => ExecuteAsync(EstunProtocol.BuildCommand(EstunProtocol.UnregisterProject), cancellationToken);
        /// <summary>전역 속도를 기록하고 적용합니다.</summary>
        public Task<OperationResult> SetGlobalSpeedAsync(short value, CancellationToken cancellationToken = default(CancellationToken)) => ExecuteAsync(EstunProtocol.BuildSetGlobalSpeed(value), cancellationToken);
        /// <summary>명령 상태를 초기화합니다.</summary>
        public Task<OperationResult> RestartCommandStatusAsync(CancellationToken cancellationToken = default(CancellationToken)) => ExecuteAsync(EstunProtocol.BuildCommand(EstunProtocol.RestartCommandStatus), cancellationToken);
        private Task<OperationResult> ExecuteAsync(EstunProtocolStep[] steps, CancellationToken token) => transport.ExecuteTransactionAsync(async tx =>
        {
            short register99 = 0, register51 = 0;
            foreach (var step in steps)
            {
                if (step.Kind == EstunStepKind.CheckIdle)
                {
                    var check = EstunProtocol.CheckIdle(register99, register51);
                    if (!check.IsSuccess)
                        return check;
                }
                else if (step.Kind == EstunStepKind.Write)
                {
                    OperationResult<byte[]> command = step.Data.Length == 2 ? ModbusCommandBuilder.BuildWriteWordModbusCommand(step.Address.ToString(), (short)(step.Data[0] * 256 + step.Data[1]), station, true, 6, new ProtocolValueConverter(ByteOrder.LittleEndianWithByteSwap)) : ModbusCommandBuilder.BuildWriteWordModbusCommand(step.Address.ToString(), step.Data, station, true, 16);
                    if (!command.IsSuccess)
                        return command;
                    var write = await ModbusTcpExchange.ExecuteAsync(tx, command.Content, transactionId++).ConfigureAwait(false);
                    if (!write.IsSuccess)
                        return write;
                }
                else
                {
                    bool matched = false;
                    for (int attempt = 0; attempt < step.MaxAttempts; attempt++)
                    {
                        if (step.DelayMilliseconds != 0 && (step.Kind == EstunStepKind.Read || attempt != 0))
                            await Task.Delay(step.DelayMilliseconds, tx.CancellationToken).ConfigureAwait(false);
                        var read = await ReadAsync(tx, step.Address, 1).ConfigureAwait(false);
                        if (!read.IsSuccess)
                            return read;
                        short value = (short)(read.Content[0] * 256 + read.Content[1]);
                        if (step.Address == 99)
                            register99 = value;
                        if (step.Address == 51)
                            register51 = value;
                        if (!step.ExpectedValue.HasValue || step.ExpectedValue.Value == value)
                        {
                            matched = true;
                            break;
                        }
                    }

                    if (!matched)
                        return new OperationResult("레지스터 18의 다운로드 승인 대기 횟수를 초과했습니다.");
                }
            }

            return OperationResult.CreateSuccessResult();
        }, timeout, token);
        private Task<OperationResult<byte[]>> ReadAsync(ITcpTransaction tx, ushort address, ushort length)
        {
            byte[] core = ModbusCommandBuilder.BuildReadModbusCommand(address.ToString(), length, station, true, 3).Content[0];
            return ModbusTcpExchange.ExecuteAsync(tx, core, transactionId++);
        }
    }
}
