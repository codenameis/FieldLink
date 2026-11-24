using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Conversion;

namespace FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;

public sealed partial class YercUdpClient
{
    private const int StringVariableLength = 16;
    private const int MaxIoBatchCount = 474;
    private const int MaxRegisterBatchCount = 237;
    private const int MaxByteBatchCount = 474;
    private const int MaxInt16BatchCount = 237;
    private const int MaxInt32BatchCount = 118;
    private const int MaxSingleBatchCount = 118;
    private const int MaxStringBatchCount = 29;
    private const int EvenByteDivisor = 2;

    /// <summary>단일 I/O 바이트(<c>0x78</c>)를 읽습니다.</summary>
    public Task<RobotResult<byte>> ReadIoByteAsync(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleIo, address, SinglePointAttribute, (byte)YercService.GetSingle), YercResponseParser.ParseSingleBytePayload, cancellationToken);

    /// <summary>단일 I/O 바이트(<c>0x78</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteIoByteAsync(ushort address, byte value, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SingleIo, address, SinglePointAttribute, (byte)YercService.SetSingle), new[] { value }, cancellationToken);

    /// <summary>배치 I/O 바이트(<c>0x300</c>)를 읽습니다. 사양서 기준 4바이트 개수 헤더를 항상 사용합니다.</summary>
    public Task<RobotResult<byte[]>> ReadIoBatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count, MaxIoBatchCount, nameof(count));
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchIo, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseByteBatch(payload, count, hasCountHeader: true),
            cancellationToken);
    }

    /// <summary>배치 I/O 바이트(<c>0x300</c>)를 씁니다. 사양서 기준 2의 배수 길이와 4바이트 개수 헤더를 항상 사용합니다.</summary>
    public Task<RobotResult> WriteIoBatchAsync(ushort startAddress, byte[] values, CancellationToken cancellationToken = default)
    {
        ValidateEvenByteBatch(values, MaxIoBatchCount);
        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.BatchIo, startAddress, 0, (byte)YercService.WriteMulti),
            YercRequestBuilder.BuildBatchWritePayload(values, values.Length, includeCountHeader: true),
            cancellationToken);
    }

    /// <summary>단일 레지스터 <c>M</c>(<c>0x79</c>)을 읽습니다.</summary>
    public Task<RobotResult<ushort>> ReadRegisterAsync(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleRegister, address, SinglePointAttribute, (byte)YercService.GetSingle), YercResponseParser.ParseUInt16Payload, cancellationToken);

    /// <summary>단일 레지스터 <c>M</c>(<c>0x79</c>)을 씁니다.</summary>
    public Task<RobotResult> WriteRegisterAsync(ushort address, ushort value, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SingleRegister, address, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildUInt16Payload(value), cancellationToken);

    /// <summary>배치 레지스터 <c>M</c>(<c>0x301</c>)을 읽습니다.</summary>
    public Task<RobotResult<ushort[]>> ReadRegisterBatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count, MaxRegisterBatchCount, nameof(count));
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchRegister, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseRegisterBatch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>배치 레지스터 <c>M</c>(<c>0x301</c>)을 씁니다.</summary>
    public Task<RobotResult> WriteRegisterBatchAsync(ushort startAddress, ushort[] values, CancellationToken cancellationToken = default)
    {
        ValidateBatchArray(values, MaxRegisterBatchCount);
        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.BatchRegister, startAddress, 0, (byte)YercService.WriteMulti),
            YercRequestBuilder.BuildUInt16BatchPayload(values, _options.IncludeBatchCountHeader),
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
        ValidateBatchCount(count, MaxByteBatchCount, nameof(count));
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchByte, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseByteBatch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>배치 바이트 변수 <c>B</c>(<c>0x302</c>)를 씁니다. (2의 배수 길이)</summary>
    public Task<RobotResult> WriteByteBatchAsync(ushort startAddress, byte[] values, CancellationToken cancellationToken = default)
    {
        ValidateEvenByteBatch(values, MaxByteBatchCount);
        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.BatchByte, startAddress, 0, (byte)YercService.WriteMulti),
            YercRequestBuilder.BuildBatchWritePayload(values, values.Length, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>단일 정수 변수 <c>I</c>(<c>0x7B</c>)를 읽습니다.</summary>
    public Task<RobotResult<short>> ReadInt16Async(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleInteger, address, SinglePointAttribute, (byte)YercService.GetSingle), YercResponseParser.ParseInt16Payload, cancellationToken);

    /// <summary>단일 정수 변수 <c>I</c>(<c>0x7B</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteInt16Async(ushort address, short value, CancellationToken cancellationToken = default) =>
        SendControlAsync(new YercCommandHeader((ushort)YercCommand.SingleInteger, address, SinglePointAttribute, (byte)YercService.SetSingle), YercRequestBuilder.BuildUInt16Payload((ushort)value), cancellationToken);

    /// <summary>배치 정수 변수 <c>I</c>(<c>0x303</c>)를 읽습니다.</summary>
    public Task<RobotResult<short[]>> ReadInt16BatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count, MaxInt16BatchCount, nameof(count));
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchInteger, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseInt16Batch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>배치 정수 변수 <c>I</c>(<c>0x303</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteInt16BatchAsync(ushort startAddress, short[] values, CancellationToken cancellationToken = default)
    {
        ValidateBatchArray(values, MaxInt16BatchCount);
        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.BatchInteger, startAddress, 0, (byte)YercService.WriteMulti),
            YercRequestBuilder.BuildInt16BatchPayload(values, _options.IncludeBatchCountHeader),
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
        ValidateBatchCount(count, MaxInt32BatchCount, nameof(count));
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchDoubleInteger, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseInt32Batch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>배치 배정수 변수 <c>D</c>(<c>0x304</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteInt32BatchAsync(ushort startAddress, int[] values, CancellationToken cancellationToken = default)
    {
        ValidateBatchArray(values, MaxInt32BatchCount);
        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.BatchDoubleInteger, startAddress, 0, (byte)YercService.WriteMulti),
            YercRequestBuilder.BuildInt32BatchPayload(values, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>단일 실수 변수 <c>R</c>(<c>0x7D</c>)을 읽습니다.</summary>
    public Task<RobotResult<float>> ReadSingleAsync(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(new YercCommandHeader((ushort)YercCommand.SingleReal, address, SinglePointAttribute, (byte)YercService.GetSingle), YercResponseParser.ParseSinglePayload, cancellationToken);

    /// <summary>단일 실수 변수 <c>R</c>(<c>0x7D</c>)을 씁니다.</summary>
    public Task<RobotResult> WriteSingleAsync(ushort address, float value, CancellationToken cancellationToken = default) =>
        SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.SingleReal, address, SinglePointAttribute, (byte)YercService.SetSingle),
            YercRequestBuilder.BuildSinglePayload(value),
            cancellationToken);

    /// <summary>배치 실수 변수 <c>R</c>(<c>0x305</c>)을 읽습니다.</summary>
    public Task<RobotResult<float[]>> ReadSingleBatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count, MaxSingleBatchCount, nameof(count));
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchReal, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseSingleBatch(payload, count, _options.IncludeBatchCountHeader),
            cancellationToken);
    }

    /// <summary>배치 실수 변수 <c>R</c>(<c>0x305</c>)을 씁니다.</summary>
    public Task<RobotResult> WriteSingleBatchAsync(ushort startAddress, float[] values, CancellationToken cancellationToken = default)
    {
        ValidateBatchArray(values, MaxSingleBatchCount);
        byte[] payload = YercRequestBuilder.BuildSingleBatchPayload(values, _options.IncludeBatchCountHeader);
        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.BatchReal, startAddress, 0, (byte)YercService.WriteMulti),
            payload,
            cancellationToken);
    }

    /// <summary>단일 문자열 변수 <c>S</c>(<c>0x7E</c>)를 읽습니다.</summary>
    public Task<RobotResult<string>> ReadStringVariableAsync(ushort address, CancellationToken cancellationToken = default) =>
        SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.SingleString, address, SinglePointAttribute, (byte)YercService.GetSingle),
            payload => RobotBinaryBuffer.ReadNullTrimmedString(payload, 0, Math.Min(payload.Length, StringVariableLength), _options.StringEncoding),
            cancellationToken);

    /// <summary>단일 문자열 변수 <c>S</c>(<c>0x7E</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteStringVariableAsync(ushort address, string value, CancellationToken cancellationToken = default)
    {
        byte[] payload = YercRequestBuilder.BuildPaddedStringPayload(value, _options.StringEncoding);
        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.SingleString, address, SinglePointAttribute, (byte)YercService.SetSingle),
            payload,
            cancellationToken);
    }

    /// <summary>배치 문자열 변수 <c>S</c>(<c>0x306</c>)를 읽습니다.</summary>
    public Task<RobotResult<string[]>> ReadStringBatchAsync(ushort startAddress, int count, CancellationToken cancellationToken = default)
    {
        ValidateBatchCount(count, MaxStringBatchCount, nameof(count));
        return SendAndParseAsync(
            new YercCommandHeader((ushort)YercCommand.BatchString, startAddress, 0, (byte)YercService.ReadMulti),
            YercRequestBuilder.BuildInt32Payload(count),
            payload => YercResponseParser.ParseStringBatch(payload, count, _options.IncludeBatchCountHeader, _options.StringEncoding),
            cancellationToken);
    }

    /// <summary>배치 문자열 변수 <c>S</c>(<c>0x306</c>)를 씁니다.</summary>
    public Task<RobotResult> WriteStringBatchAsync(ushort startAddress, string[] values, CancellationToken cancellationToken = default)
    {
        ValidateBatchArray(values, MaxStringBatchCount);
        byte[] payload = YercRequestBuilder.BuildStringBatchPayload(values, _options.StringEncoding, _options.IncludeBatchCountHeader);
        return SendControlAsync(
            new YercCommandHeader((ushort)YercCommand.BatchString, startAddress, 0, (byte)YercService.WriteMulti),
            payload,
            cancellationToken);
    }

    private static void ValidateBatchCount(int count, int maximumCount, string parameterName)
    {
        if (count < 1 || count > maximumCount)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                count,
                $"YERC 배치 요소 수는 1에서 {maximumCount} 사이여야 합니다.");
        }
    }

    private static void ValidateBatchArray<T>(T[] values, int maximumCount)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        ValidateBatchCount(values.Length, maximumCount, nameof(values));
    }

    private static void ValidateEvenByteBatch(byte[] values, int maximumCount)
    {
        ValidateBatchArray(values, maximumCount);
        if (values.Length % EvenByteDivisor != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                values.Length,
                $"YERC 바이트 배치 쓰기 길이는 {EvenByteDivisor}의 배수여야 합니다.");
        }
    }
}
