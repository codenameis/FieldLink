using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

public sealed partial class YercUdpClient
{
    private const ushort ActiveAlarmStartInstance = 1;
    private const ushort ActiveAlarmEndInstance = 4;
    private const ushort DefaultPoseInstance = 101;
    private const ushort DefaultTorqueInstance = 21;
    private const ushort DefaultSystemInfoId = 11;
    private const ushort DefaultTaskInstance = 1;
    private const ushort ResetAlarmInstance = 1;
    private const ushort CancelErrorInstance = 2;
    private const ushort HoldInstance = 1;
    private const ushort ServoPowerInstance = 2;
    private const ushort HoldLockInstance = 3;
    private const ushort CycleInstance = 2;
    private const ushort DisplayMessageInstance = 1;
    private const ushort StartJobInstance = 1;
    private const ushort SelectJobInstance = 1;
    private const byte ManagementTimeAttribute = 1;
    private const byte ManagementTimespanAttribute = 2;
    private const int ControlCommandTriggerValue = 1;
    private const int CommandOnValue = 1;
    private const int CommandOffValue = 2;
    private const int MinimumCycleValue = 1;
    private const int MaximumCycleValue = 3;

    /// <summary>현재 활성 알람(인스턴스 1~4) 목록을 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyList<YercAlarmItem>>> ReadAlarmsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                var alarms = new List<YercAlarmItem>();
                for (ushort instance = ActiveAlarmStartInstance; instance <= ActiveAlarmEndInstance; instance++)
                {
                    byte[] payload = await ExchangePayloadAsync(
                        new YercCommandHeader((ushort)YercCommand.ReadAlarm, instance, 0, (byte)YercService.GetAll),
                        null,
                        token).ConfigureAwait(false);
                    YercAlarmItem? item = YercResponseParser.ParseAlarm(payload, _options.StringEncoding);
                    if (item is not null)
                    {
                        alarms.Add(item);
                    }
                }

                return (IReadOnlyList<YercAlarmItem>)alarms.AsReadOnly();
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>지정 시작 인스턴스부터 연속된 이력 알람을 조회합니다. (사양서 §8.2 인덱스 증가 반영)</summary>
    public Task<RobotResult<IReadOnlyList<YercAlarmItem>>> ReadHistoryAlarmsAsync(
        ushort startInstanceId,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "조회 개수는 0 이상이어야 합니다.");
        }

        if (startInstanceId + (long)count - 1 > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "이력 알람 인스턴스 번호가 16비트 범위를 초과합니다.");
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                var alarms = new List<YercAlarmItem>(count);
                for (int i = 0; i < count; i++)
                {
                    ushort instance = checked((ushort)(startInstanceId + i));
                    byte[] payload = await ExchangePayloadAsync(
                        new YercCommandHeader((ushort)YercCommand.ReadHistoryAlarm, instance, 0, (byte)YercService.GetAll),
                        null,
                        token).ConfigureAwait(false);
                    YercAlarmItem? item = YercResponseParser.ParseAlarm(payload, _options.StringEncoding);
                    if (item is not null)
                    {
                        alarms.Add(item);
                    }
                }

                return (IReadOnlyList<YercAlarmItem>)alarms.AsReadOnly();
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>로봇 상태 16비트(<c>0x72</c>)를 조회합니다.</summary>
    public Task<RobotResult<bool[]>> ReadStatusBitsAsync(CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.ReadStats, DefaultTaskInstance, 0, (byte)YercService.GetAll), YercResponseParser.ParseStatusBits, cancellationToken);

    /// <summary>실행 JOB 정보(<c>0x73</c>)를 조회합니다.</summary>
    public Task<RobotResult<YercJobSequence>> ReadJobSequenceAsync(ushort task = DefaultTaskInstance, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.ReadJobSequence, task, 0, (byte)YercService.GetAll), payload => YercResponseParser.ParseJobSequence(payload, _options.StringEncoding), cancellationToken);

    /// <summary>현재 직교 자세(<c>0x75</c>, Instance=101)를 조회합니다.</summary>
    public Task<RobotResult<YercPose>> ReadPoseAsync(CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.ReadPose, DefaultPoseInstance, 0, (byte)YercService.GetAll), YercResponseParser.ParsePose, cancellationToken);

    /// <summary>각 축 토크(<c>0x77</c>, Instance=21)를 조회합니다.</summary>
    public Task<RobotResult<int[]>> ReadTorqueAsync(CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.ReadTorque, DefaultTorqueInstance, 0, (byte)YercService.GetAll), YercResponseParser.ParseTorque, cancellationToken);

    /// <summary>관리 시각 문자열(<c>0x88</c>, Attr=1)을 조회합니다.</summary>
    public Task<RobotResult<string>> ReadManagementTimeAsync(ushort id, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.ManagementTime, id, ManagementTimeAttribute, (byte)YercService.GetSingle), payload => RobotBinaryBuffer.ReadNullTrimmedString(payload, 0, payload.Length, _options.StringEncoding), cancellationToken);

    /// <summary>관리 경과 시간 문자열(<c>0x88</c>, Attr=2)을 조회합니다.</summary>
    public Task<RobotResult<string>> ReadManagementTimespanAsync(ushort id, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.ManagementTime, id, ManagementTimespanAttribute, (byte)YercService.GetSingle), payload => RobotBinaryBuffer.ReadNullTrimmedString(payload, 0, payload.Length, _options.StringEncoding), cancellationToken);

    /// <summary>시스템 정보(<c>0x89</c>)를 조회합니다.</summary>
    public Task<RobotResult<YercSystemInfo>> ReadSystemInfoAsync(ushort systemId = DefaultSystemInfoId, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SystemInfo, systemId, 0, (byte)YercService.GetAll), YercResponseParser.ParseSystemInfo, cancellationToken);

    /// <summary>알람 리셋(<c>0x82/1</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> ResetAsync(CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.ResetOrCancel, ResetAlarmInstance, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(ControlCommandTriggerValue), cancellationToken);

    /// <summary>에러 취소(<c>0x82/2</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> CancelAsync(CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.ResetOrCancel, CancelErrorInstance, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(ControlCommandTriggerValue), cancellationToken);

    /// <summary>HOLD ON/OFF(<c>0x83/1</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> HoldAsync(bool on, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.HoldServoOrLock, HoldInstance, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(on ? CommandOnValue : CommandOffValue), cancellationToken);

    /// <summary>서보 전원 ON/OFF(<c>0x83/2</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> SetServoPowerAsync(bool on, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.HoldServoOrLock, ServoPowerInstance, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(on ? CommandOnValue : CommandOffValue), cancellationToken);

    /// <summary>인터락 ON/OFF(<c>0x83/3</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> SetHoldLockAsync(bool on, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.HoldServoOrLock, HoldLockInstance, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(on ? CommandOnValue : CommandOffValue), cancellationToken);

    /// <summary>사이클 선택(<c>0x84/2</c>: 1=Step, 2=1-Cycle, 3=Auto) 명령을 전송합니다.</summary>
    public Task<RobotResult> SetCycleAsync(int cycle, CancellationToken cancellationToken = default)
    {
        if (cycle is < MinimumCycleValue or > MaximumCycleValue)
        {
            throw new ArgumentOutOfRangeException(nameof(cycle), cycle, $"사이클 모드는 {MinimumCycleValue}(Step)에서 {MaximumCycleValue}(Auto) 사이여야 합니다.");
        }

        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.Cycle, CycleInstance, SinglePointAttribute, (byte)YercService.SetSingle),
            YercRequestBuilder.BuildInt32Payload(cycle),
            cancellationToken);
    }

    /// <summary>티칭 펜던트 메시지(<c>0x85/1</c>)를 표시합니다.</summary>
    public Task<RobotResult> DisplayMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.DisplayMessage, DisplayMessageInstance, SinglePointAttribute, (byte)YercService.SetSingle),
            _options.StringEncoding.GetBytes(message),
            cancellationToken);
    }

    /// <summary>JOB 시작(<c>0x86/1</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> StartJobAsync(CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.StartJob, StartJobInstance, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(ControlCommandTriggerValue), cancellationToken);

    /// <summary>실행 JOB 및 라인 선택(<c>0x87/Inst=1/Attr=0/Svc=0x02</c>) 명령을 전송합니다. (사양서 §8.2 적용)</summary>
    public Task<RobotResult> SelectJobAsync(string jobName, int lineNumber, CancellationToken cancellationToken = default)
    {
        byte[] payload = YercRequestBuilder.BuildSelectJobPayload(jobName, lineNumber, _options.StringEncoding);
        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.SelectJob, SelectJobInstance, 0, (byte)YercService.SetAll),
            payload,
            cancellationToken);
    }
}
