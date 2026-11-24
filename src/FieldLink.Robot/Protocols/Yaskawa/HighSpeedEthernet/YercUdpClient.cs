using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Transport;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

/// <summary>YASKAWA High-Speed Ethernet (<c>YERC</c>) UDP 바이너리 프로토콜 클라이언트입니다.</summary>
public sealed class YercUdpClient : IRobotClient
{
    private const int StringVariableLength = 16;
    private const ushort ActiveAlarmStartInstance = 1;
    private const ushort ActiveAlarmEndInstance = 4;
    private const ushort DefaultPoseInstance = 101;
    private const ushort DefaultTorqueInstance = 21;
    private const ushort DefaultSystemInfoId = 11;
    private const ushort DefaultTaskInstance = 1;
    private const byte SinglePointAttribute = 1;
    private const byte ManagementTimeAttribute = 1;
    private const byte ManagementTimespanAttribute = 2;
    private const int ControlCommandTriggerValue = 1;
    private const int CommandOnValue = 1;
    private const int CommandOffValue = 2;
    private const int MinimumAddressLength = 2;

    private readonly ITransport _transport;
    private readonly YercUdpOptions _options;
    private readonly YercFrameDecoder _frameDecoder = new();
    private readonly object _requestIdLock = new();

    private byte _requestId;
    private bool _isOpen;
    private bool _isDisposed;

    /// <summary>전송 구현체와 옵션으로 High-Speed Ethernet 클라이언트를 초기화합니다.</summary>
    public YercUdpClient(ITransport transport, YercUdpOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new YercUdpOptions();
    }

    /// <inheritdoc />
    public bool IsOpen => !_isDisposed && _isOpen && _transport.State == TransportState.Open;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (IsOpen)
        {
            throw new InvalidOperationException("YASKAWA High-Speed UDP 클라이언트가 이미 열려 있습니다.");
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                await _transport.OpenAsync(token).ConfigureAwait(false);
                _isOpen = true;
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        if (!_isOpen)
        {
            return Task.FromResult(RobotResult.Success());
        }

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                _isOpen = false;
                await _transport.CloseAsync(token).ConfigureAwait(false);
            },
            _options.Timeout,
            cancellationToken);
    }

    /// <summary>임의 <c>YERC</c> 명령을 전송하고 응답 페이로드 바이트를 반환합니다.</summary>
    public Task<RobotResult<byte[]>> SendCommandAsync(
        YercCommandHeader header,
        byte[]? dataPart = null,
        CancellationToken cancellationToken = default)
    {
        if (header is null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        ThrowIfDisposed();
        EnsureOpen();

        return RobotErrorMapper.ExecuteAsync(
            token => ExchangePayloadAsync(header, dataPart, token),
            _options.Timeout,
            cancellationToken);
    }

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

    /// <summary>단일 I/O 바이트(<c>0x78</c>)를 읽습니다.</summary>
    public Task<RobotResult<byte>> ReadIoByteAsync(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleIo, address, SinglePointAttribute, (byte)YercService.GetSingle), YercResponseParser.ParseSingleBytePayload, cancellationToken);

    /// <summary>단일 I/O 바이트(<c>0x78</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteIoByteAsync(ushort address, byte value, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SingleIo, address, SinglePointAttribute, (byte)YercService.SetSingle), new[] { value }, cancellationToken);

    /// <summary>배치 I/O 바이트(<c>0x300</c>)를 읽습니다.</summary>
    public Task<RobotResult<byte[]>> ReadIoBatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count);
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchIo, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseByteBatch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>배치 I/O 바이트(<c>0x300</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteIoBatchAsync(ushort startAddress, byte[] values, CancellationToken cancellationToken = default)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.BatchIo, startAddress, 0, (byte)YercService.WriteMulti),
            YercRequestBuilder.BuildBatchWritePayload(values, values.Length, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>단일 레지스터 <c>M</c>(<c>0x79</c>)을 읽습니다.</summary>
    public Task<RobotResult<ushort>> ReadRegisterAsync(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleRegister, address, SinglePointAttribute, (byte)YercService.GetSingle), YercResponseParser.ParseUInt16Payload, cancellationToken);

    /// <summary>단일 레지스터 <c>M</c>(<c>0x79</c>)을 씁니다.</summary>
    public Task<RobotResult> WriteRegisterAsync(ushort address, ushort value, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SingleRegister, address, SinglePointAttribute, (byte)YercService.SetSingle), EncodeUInt16(value), cancellationToken);

    /// <summary>배치 레지스터 <c>M</c>(<c>0x301</c>)을 읽습니다.</summary>
    public Task<RobotResult<ushort[]>> ReadRegisterBatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count);
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchRegister, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseRegisterBatch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>배치 레지스터 <c>M</c>(<c>0x301</c>)을 씁니다.</summary>
    public Task<RobotResult> WriteRegisterBatchAsync(ushort startAddress, ushort[] values, CancellationToken cancellationToken = default)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.BatchRegister, startAddress, 0, (byte)YercService.WriteMulti),
            EncodeUInt16Batch(values),
            cancellationToken);
    }

    /// <summary>단일 바이트 변수 <c>B</c>(<c>0x7A</c>)를 읽습니다.</summary>
    public Task<RobotResult<byte>> ReadByteAsync(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleByte, address, SinglePointAttribute, (byte)YercService.GetSingle), YercResponseParser.ParseSingleBytePayload, cancellationToken);

    /// <summary>단일 바이트 변수 <c>B</c>(<c>0x7A</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteByteAsync(ushort address, byte value, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SingleByte, address, SinglePointAttribute, (byte)YercService.SetSingle), new[] { value }, cancellationToken);

    /// <summary>배치 바이트 변수 <c>B</c>(<c>0x302</c>)를 읽습니다.</summary>
    public Task<RobotResult<byte[]>> ReadByteBatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count);
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchByte, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseByteBatch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>단일 정수 변수 <c>I</c>(<c>0x7B</c>)를 읽습니다.</summary>
    public Task<RobotResult<short>> ReadInt16Async(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleInteger, address, SinglePointAttribute, (byte)YercService.GetSingle), YercResponseParser.ParseInt16Payload, cancellationToken);

    /// <summary>단일 정수 변수 <c>I</c>(<c>0x7B</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteInt16Async(ushort address, short value, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SingleInteger, address, SinglePointAttribute, (byte)YercService.SetSingle), EncodeUInt16((ushort)value), cancellationToken);

    /// <summary>배치 정수 변수 <c>I</c>(<c>0x303</c>)를 읽습니다.</summary>
    public Task<RobotResult<short[]>> ReadInt16BatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count);
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchInteger, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseInt16Batch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>단일 배정수 변수 <c>D</c>(<c>0x7C</c>)를 읽습니다.</summary>
    public Task<RobotResult<int>> ReadInt32Async(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleDoubleInteger, address, SinglePointAttribute, (byte)YercService.GetSingle), YercResponseParser.ParseInt32Payload, cancellationToken);

    /// <summary>단일 배정수 변수 <c>D</c>(<c>0x7C</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteInt32Async(ushort address, int value, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SingleDoubleInteger, address, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(value), cancellationToken);

    /// <summary>배치 배정수 변수 <c>D</c>(<c>0x304</c>)를 읽습니다.</summary>
    public Task<RobotResult<int[]>> ReadInt32BatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count);
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchDoubleInteger, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseInt32Batch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>단일 실수 변수 <c>R</c>(<c>0x7D</c>)을 읽습니다.</summary>
    public Task<RobotResult<float>> ReadSingleAsync(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleReal, address, SinglePointAttribute, (byte)YercService.GetSingle), YercResponseParser.ParseSinglePayload, cancellationToken);

    /// <summary>단일 실수 변수 <c>R</c>(<c>0x7D</c>)을 씁니다.</summary>
    public Task<RobotResult> WriteSingleAsync(ushort address, float value, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SingleReal, address, SinglePointAttribute, (byte)YercService.SetSingle), EncodeSingle(value), cancellationToken);

    /// <summary>배치 실수 변수 <c>R</c>(<c>0x305</c>)을 읽습니다.</summary>
    public Task<RobotResult<float[]>> ReadSingleBatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count);
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchReal, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseSingleBatch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>단일 문자열 변수 <c>S</c>(<c>0x7E</c>)를 읽습니다.</summary>
    public Task<RobotResult<string>> ReadStringVariableAsync(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleString, address, SinglePointAttribute, (byte)YercService.GetSingle), payload => RobotBinaryBuffer.ReadNullTrimmedString(payload, 0, Math.Min(payload.Length, StringVariableLength), _options.StringEncoding), cancellationToken);

    /// <summary>단일 문자열 변수 <c>S</c>(<c>0x7E</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteStringVariableAsync(ushort address, string value, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SingleString, address, SinglePointAttribute, (byte)YercService.SetSingle), EncodePaddedString(value), cancellationToken);

    /// <summary>배치 문자열 변수 <c>S</c>(<c>0x306</c>)를 읽습니다.</summary>
    public Task<RobotResult<string[]>> ReadStringBatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count);
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchString, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseStringBatch(payload, count, _options.IncludeBatchCountHeader, _options.StringEncoding),
            cancellationToken);
    }

    /// <summary>알람 리셋(<c>0x82/1</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> ResetAsync(CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.ResetOrCancel, 1, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(ControlCommandTriggerValue), cancellationToken);

    /// <summary>에러 취소(<c>0x82/2</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> CancelAsync(CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.ResetOrCancel, 2, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(ControlCommandTriggerValue), cancellationToken);

    /// <summary>HOLD ON/OFF(<c>0x83/1</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> HoldAsync(bool on, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.HoldServoOrLock, 1, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(on ? CommandOnValue : CommandOffValue), cancellationToken);

    /// <summary>서보 전원 ON/OFF(<c>0x83/2</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> SetServoPowerAsync(bool on, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.HoldServoOrLock, 2, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(on ? CommandOnValue : CommandOffValue), cancellationToken);

    /// <summary>인터락 ON/OFF(<c>0x83/3</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> SetHoldLockAsync(bool on, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.HoldServoOrLock, 3, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(on ? CommandOnValue : CommandOffValue), cancellationToken);

    /// <summary>사이클 선택(<c>0x84/2</c>: 1=Step, 2=1-Cycle, 3=Auto) 명령을 전송합니다.</summary>
    public Task<RobotResult> SetCycleAsync(int cycle, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.Cycle, 2, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(cycle), cancellationToken);

    /// <summary>티칭 펜던트 메시지(<c>0x85/1</c>)를 표시합니다.</summary>
    public Task<RobotResult> DisplayMessageAsync(string message, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.DisplayMessage, 1, SinglePointAttribute, (byte)YercService.SetSingle), _options.StringEncoding.GetBytes(message ?? string.Empty), cancellationToken);

    /// <summary>JOB 시작(<c>0x86/1</c>) 명령을 전송합니다.</summary>
    public Task<RobotResult> StartJobAsync(CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.StartJob, 1, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildInt32Payload(ControlCommandTriggerValue), cancellationToken);

    /// <summary>실행 JOB 및 라인 선택(<c>0x87/Inst=1/Attr=0/Svc=0x02</c>) 명령을 전송합니다. (사양서 §8.2 적용)</summary>
    public Task<RobotResult> SelectJobAsync(string jobName, int lineNumber, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SelectJob, 1, 0, (byte)YercService.SetAll), YercRequestBuilder.BuildSelectJobPayload(jobName, lineNumber, _options.StringEncoding), cancellationToken);

    /// <inheritdoc />
    public Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default) =>
        SendCommandAsync(ParseAddressHeader(address, isWrite: false), null, cancellationToken);

    /// <inheritdoc />
    public async Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        RobotResult<byte[]> raw = await ReadRawAsync(address, cancellationToken).ConfigureAwait(false);
        return raw.IsSuccess
            ? RobotResult.Success(BitConverter.ToString(raw.Value))
            : RobotResult.Failure<string>(raw.Error);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return SendControlAsync(ParseAddressHeader(address, isWrite: true), data, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteStringAsync(string address, string value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return WriteRawAsync(address, _options.StringEncoding.GetBytes(value), cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isOpen = false;
        _transport.Dispose();
    }

    private Task<RobotResult<T>> SendAndParseAsync<T>(YercCommandHeader header, Func<byte[], T> parser, CancellationToken cancellationToken) =>
        SendAndParseAsync(header, null, parser, cancellationToken);

    private Task<RobotResult<T>> SendAndParseAsync<T>(YercCommandHeader header, byte[]? dataPart, Func<byte[], T> parser, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return RobotErrorMapper.ExecuteAsync(
            async token => parser(await ExchangePayloadAsync(header, dataPart, token).ConfigureAwait(false)),
            _options.Timeout,
            cancellationToken);
    }

    private Task<RobotResult> SendControlAsync(YercCommandHeader header, byte[]? dataPart, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return RobotErrorMapper.ExecuteAsync(
            async token => { await ExchangePayloadAsync(header, dataPart, token).ConfigureAwait(false); },
            _options.Timeout,
            cancellationToken);
    }

    private Task<byte[]> ExchangePayloadAsync(YercCommandHeader header, byte[]? dataPart, CancellationToken cancellationToken) =>
        _transport.RunExclusiveAsync(
            async session =>
            {
                byte requestId = NextRequestId();
                byte[] request = YercRequestBuilder.BuildRequest(header, requestId, dataPart);
                await session.SendAsync(request).ConfigureAwait(false);
                byte[] response = await session.ReceiveAsync(_frameDecoder).ConfigureAwait(false);
                return YercResponseParser.ExtractPayload(response, requestId);
            },
            cancellationToken);

    private byte NextRequestId()
    {
        lock (_requestIdLock)
        {
            _requestId++;
            return _requestId;
        }
    }

    private static YercCommandHeader ParseAddressHeader(string address, bool isWrite)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        string upper = address.Trim().ToUpperInvariant();
        if (upper.Length >= MinimumAddressLength && ushort.TryParse(upper.Substring(1), out ushort instance))
        {
            byte service = isWrite ? (byte)YercService.SetSingle : (byte)YercService.GetSingle;
            return upper[0] switch
            {
                'M' => new YercCommandHeader((ushort)YercCommand.SingleRegister, instance, SinglePointAttribute, service),
                'B' => new YercCommandHeader((ushort)YercCommand.SingleByte, instance, SinglePointAttribute, service),
                'I' => new YercCommandHeader((ushort)YercCommand.SingleInteger, instance, SinglePointAttribute, service),
                'D' => new YercCommandHeader((ushort)YercCommand.SingleDoubleInteger, instance, SinglePointAttribute, service),
                'R' => new YercCommandHeader((ushort)YercCommand.SingleReal, instance, SinglePointAttribute, service),
                'S' => new YercCommandHeader((ushort)YercCommand.SingleString, instance, SinglePointAttribute, service),
                _ => throw new FormatException($"지원하지 않는 YERC 변수 주소입니다: '{address}'.")
            };
        }

        throw new FormatException($"유효하지 않은 YERC 변수 주소입니다: '{address}'.");
    }

    private static void ValidateBatchCount(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "배치 개수는 0 이상이어야 합니다.");
        }
    }

    private byte[] EncodePaddedString(string value)
    {
        byte[] padded = new byte[StringVariableLength];
        byte[] bytes = _options.StringEncoding.GetBytes(value);
        Array.Copy(bytes, 0, padded, 0, Math.Min(bytes.Length, StringVariableLength));
        return padded;
    }

    private byte[] EncodeUInt16Batch(ushort[] values)
    {
        byte[] raw = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            RobotBinaryBuffer.WriteUInt16LittleEndian(raw, i * 2, values[i]);
        }

        return YercRequestBuilder.BuildBatchWritePayload(raw, values.Length, _options.IncludeBatchCountHeader);
    }

    private static byte[] EncodeUInt16(ushort value)
    {
        byte[] bytes = new byte[2];
        RobotBinaryBuffer.WriteUInt16LittleEndian(bytes, 0, value);
        return bytes;
    }

    private static byte[] EncodeSingle(float value)
    {
        byte[] bytes = new byte[4];
        RobotBinaryBuffer.WriteSingleLittleEndian(bytes, 0, value);
        return bytes;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("YASKAWA High-Speed UDP 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(YercUdpClient));
        }
    }
}
