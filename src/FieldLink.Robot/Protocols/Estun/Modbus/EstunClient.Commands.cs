using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

public sealed partial class EstunClient
{
    private const ushort CommandStatusRegisterAddress = 18;
    private const ushort CommandRegisterAddress = 51;
    private const ushort GlobalSpeedRegisterAddress = 52;
    private const ushort ProjectNameRegisterAddress = 53;
    private const ushort AuthorityFlagRegisterAddress = 99;
    private const short AcquireAuthorityValue = 0x0011;
    private const short ClearValue = 0x0000;
    private const ushort AuthorityReadyStatus = 0x0801;
    private const int BytesPerRegister = 2;

    /// <summary>프로그램 시작 명령을 실행합니다.</summary>
    public Task<RobotResult> StartProgramAsync(CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(EstunCommandCode.StartProgram, null, cancellationToken);

    /// <summary>프로그램 정지 명령을 실행합니다.</summary>
    public Task<RobotResult> StopProgramAsync(CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(EstunCommandCode.StopProgram, null, cancellationToken);

    /// <summary>에러 리셋 명령을 실행합니다.</summary>
    public Task<RobotResult> ResetErrorAsync(CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(EstunCommandCode.ResetError, null, cancellationToken);

    /// <summary>지정된 이름의 프로젝트를 로드합니다.</summary>
    public Task<RobotResult> LoadProjectAsync(string projectName, CancellationToken cancellationToken = default)
    {
        byte[] projectBytes = EstunModbusRequestBuilder.BuildProjectNamePayload(projectName, _options.SwapProjectNameWriteBytes);
        return RunHandshakeCommandAsync(
            EstunCommandCode.LoadProject,
            (session, nextTransactionId) => WriteMultipleRegistersAsync(session, nextTransactionId(), ProjectNameRegisterAddress, projectBytes),
            cancellationToken);
    }

    /// <summary>현재 로드된 프로젝트를 해제합니다.</summary>
    public Task<RobotResult> UnregisterProjectAsync(CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(EstunCommandCode.UnregisterProject, null, cancellationToken);

    /// <summary>글로벌 속도 비율을 설정합니다.</summary>
    public Task<RobotResult> SetGlobalSpeedAsync(short speed, CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(
            EstunCommandCode.SetGlobalSpeed,
            (session, nextTransactionId) => WriteSingleRegisterAsync(session, nextTransactionId(), GlobalSpeedRegisterAddress, speed),
            cancellationToken);

    /// <summary>명령 상태 재시작 명령을 실행합니다.</summary>
    public Task<RobotResult> RestartCommandStatusAsync(CancellationToken cancellationToken = default) =>
        RunHandshakeCommandAsync(EstunCommandCode.RestartCommandStatus, null, cancellationToken);

    private Task<RobotResult> RunHandshakeCommandAsync(
        EstunCommandCode command,
        Func<ITransportSession, Func<ushort>, Task>? preWriteAction,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                await _transport.RunExclusiveAsync(
                    async session =>
                    {
                        ushort GetNextTransactionId() => NextTransactionId(session.ConnectionId);
                        if (preWriteAction is not null)
                        {
                            await preWriteAction(session, GetNextTransactionId).ConfigureAwait(false);
                        }

                        await ExecuteSixStepHandshakeAsync(session, GetNextTransactionId, command).ConfigureAwait(false);
                        return true;
                    },
                    token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    private async Task ExecuteSixStepHandshakeAsync(
        ITransportSession session,
        Func<ushort> nextTransactionId,
        EstunCommandCode command)
    {
        // 사양서 03 §2.2 Step 1: 권한 플래그와 명령 레지스터가 모두 0인지 선행 확인
        ushort flag = await ReadSingleRegisterAsync(session, nextTransactionId(), AuthorityFlagRegisterAddress).ConfigureAwait(false);
        if (flag != 0)
        {
            throw new RobotException(RobotErrorKind.DeviceError, "다른 클라이언트가 제어 권한을 점유 중이거나 이전 명령이 정리되지 않았습니다.");
        }

        ushort currentCommand = await ReadSingleRegisterAsync(session, nextTransactionId(), CommandRegisterAddress).ConfigureAwait(false);
        if (currentCommand != 0)
        {
            throw new RobotException(RobotErrorKind.DeviceError, "명령 레지스터가 아직 0으로 초기화되지 않았습니다.");
        }

        // Step 2: 다운로드 권한 요청 (Reg 99 = 0x0011)
        await WriteSingleRegisterAsync(session, nextTransactionId(), AuthorityFlagRegisterAddress, AcquireAuthorityValue).ConfigureAwait(false);

        bool completedSteps = false;
        try
        {
            // Step 3~5: 권한 승인 폴링 후 명령 기록, 대기 및 상태 확인
            await WaitForAuthorityAsync(session, nextTransactionId).ConfigureAwait(false);
            await WriteSingleRegisterAsync(session, nextTransactionId(), CommandRegisterAddress, (short)command).ConfigureAwait(false);
            await Task.Delay(_options.HandshakePollInterval, session.CancellationToken).ConfigureAwait(false);
            await ReadSingleRegisterAsync(session, nextTransactionId(), CommandStatusRegisterAddress).ConfigureAwait(false);
            completedSteps = true;
        }
        finally
        {
            if (completedSteps)
            {
                // Step 6: 정상 완료 시 권한 및 명령 레지스터 해제 (실패 시 사양서 §7에 따라 오류 전파)
                await WriteSingleRegisterAsync(session, nextTransactionId(), AuthorityFlagRegisterAddress, ClearValue).ConfigureAwait(false);
                await WriteSingleRegisterAsync(session, nextTransactionId(), CommandRegisterAddress, ClearValue).ConfigureAwait(false);
            }
            else
            {
                await TryReleaseAuthorityBestEffortAsync(session, nextTransactionId).ConfigureAwait(false);
            }
        }
    }

    private async Task TryReleaseAuthorityBestEffortAsync(ITransportSession session, Func<ushort> nextTransactionId)
    {
        try
        {
            await WriteSingleRegisterAsync(session, nextTransactionId(), AuthorityFlagRegisterAddress, ClearValue).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Step 3~5 실패로 이미 예외가 전파 중이므로 원래 실패 원인을 보존하고 명령 레지스터 정리를 계속 시도한다.
        }

        try
        {
            await WriteSingleRegisterAsync(session, nextTransactionId(), CommandRegisterAddress, ClearValue).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 최선 노력 정리 실패가 Step 3~5의 원래 예외를 덮지 않도록 무시한다.
        }
    }

    private async Task WaitForAuthorityAsync(ITransportSession session, Func<ushort> nextTransactionId)
    {
        for (int attempt = 0; attempt < _options.MaxAuthorityPollAttempts; attempt++)
        {
            ushort status = await ReadSingleRegisterAsync(session, nextTransactionId(), CommandStatusRegisterAddress).ConfigureAwait(false);
            if (status == AuthorityReadyStatus)
            {
                return;
            }

            if (attempt + 1 < _options.MaxAuthorityPollAttempts)
            {
                await Task.Delay(_options.HandshakePollInterval, session.CancellationToken).ConfigureAwait(false);
            }
        }

        throw new RobotException(
            RobotErrorKind.Timeout,
            $"Estun 컨트롤러가 {_options.MaxAuthorityPollAttempts}회 시도 내에 원격 제어 권한(0x0801)을 승인하지 않았습니다.");
    }

    private async Task<ushort> ReadSingleRegisterAsync(ITransportSession session, ushort transactionId, ushort address)
    {
        byte[] data = await ReadHoldingRegistersAsync(session, transactionId, address, 1).ConfigureAwait(false);
        return RobotBinaryBuffer.ReadUInt16BigEndian(data, 0);
    }

    private async Task<byte[]> ReadHoldingRegistersAsync(ITransportSession session, ushort transactionId, ushort startAddress, ushort quantity)
    {
        byte[] request = EstunModbusRequestBuilder.BuildReadHoldingRegisters(transactionId, _options.Station, startAddress, quantity);
        await session.SendAsync(request).ConfigureAwait(false);
        byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
        return EstunModbusResponseParser.ParseReadHoldingRegisters(response, transactionId, _options.Station, quantity);
    }

    private async Task WriteSingleRegisterAsync(ITransportSession session, ushort transactionId, ushort address, short value)
    {
        byte[] request = EstunModbusRequestBuilder.BuildWriteSingleRegister(transactionId, _options.Station, address, value);
        await session.SendAsync(request).ConfigureAwait(false);
        byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
        EstunModbusResponseParser.ValidateWriteSingleRegister(response, transactionId, _options.Station, address, value);
    }

    private async Task WriteMultipleRegistersAsync(ITransportSession session, ushort transactionId, ushort startAddress, byte[] registerBytes)
    {
        byte[] request = EstunModbusRequestBuilder.BuildWriteMultipleRegisters(transactionId, _options.Station, startAddress, registerBytes);
        await session.SendAsync(request).ConfigureAwait(false);
        byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
        ushort quantity = (ushort)(registerBytes.Length / BytesPerRegister);
        EstunModbusResponseParser.ValidateWriteMultipleRegisters(response, transactionId, _options.Station, startAddress, quantity);
    }
}
