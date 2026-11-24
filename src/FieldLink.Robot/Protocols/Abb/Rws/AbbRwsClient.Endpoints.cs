using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.Protocols.Abb.Rws;

public sealed partial class AbbRwsClient
{
    private const string DefaultMechunit = "ROB_1";
    private const string DefaultIoNetwork = "Local";
    private const string DefaultServoUnit = "DRV_1";
    private const string DefaultServoSignal = "DRV1K1";
    private const string CtrlStateAddress = "CtrlState";
    private const string ErrorStateAddress = "ErrorState";
    private const string SpeedRatioAddress = "SpeedRatio";
    private const string OperationModeAddress = "OperationMode";
    private const string RapidExecutionAddress = "RapidExecution";
    private const string RapidTasksAddress = "RapidTasks";
    private const string SystemAddress = "system";
    private const string InputDataClassName = "indata";
    private const string OutputDataClassName = "outdata";
    private const string RobTargetItemClassName = "ms-robtargets";
    private const string IoSignalItemClassName = "ios-signal";
    private const string RapidExecutionItemClassName = "rap-execution";
    private const string RapidTaskItemClassName = "rap-task-li";
    private const string EventLogItemClassName = "elog-message-li";
    private const string SystemItemClassName = "sys-system-li";
    private const string RapidValueClassName = "value";
    private const int DefaultEventLogMaxCount = 10;

    /// <summary>컨트롤러 상태(<c>CtrlState</c>)를 조회합니다.</summary>
    public Task<RobotResult<string>> GetCtrlStateAsync(CancellationToken cancellationToken = default) =>
        ReadStringAsync(CtrlStateAddress, cancellationToken);

    /// <summary>모션 시스템 오류 상태(<c>ErrorState</c>)를 조회합니다.</summary>
    public Task<RobotResult<string>> GetErrorStateAsync(CancellationToken cancellationToken = default) =>
        ReadStringAsync(ErrorStateAddress, cancellationToken);

    /// <summary>지정 기구 단위의 관절 목표 각도를 조회합니다.</summary>
    public Task<RobotResult<double[]>> GetJointTargetAsync(string mechunit = DefaultMechunit, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildJointTargetRequest(mechunit),
            AbbRwsResponseParser.ParseJointTarget,
            cancellationToken);
    }

    /// <summary>지정 기구 단위의 직교 목표 좌표를 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyDictionary<string, string>>> GetRobotTargetAsync(
        string mechunit = DefaultMechunit,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRobotTargetRequest(mechunit),
            body => AbbRwsResponseParser.ParseObjectByClass(body, RobTargetItemClassName),
            cancellationToken);
    }

    /// <summary>속도 비율(%)을 조회합니다.</summary>
    public Task<RobotResult<string>> GetSpeedRatioAsync(CancellationToken cancellationToken = default) =>
        ReadStringAsync(SpeedRatioAddress, cancellationToken);

    /// <summary>운전 모드를 조회합니다.</summary>
    public Task<RobotResult<string>> GetOperationModeAsync(CancellationToken cancellationToken = default) =>
        ReadStringAsync(OperationModeAddress, cancellationToken);

    /// <summary>지정 IO 장치의 입력 또는 출력 값을 조회합니다.</summary>
    public Task<RobotResult<string>> GetDeviceIoAsync(
        string deviceName,
        bool isOutput = false,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        string className = isOutput ? OutputDataClassName : InputDataClassName;
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildDeviceIoRequest(deviceName),
            body => AbbRwsResponseParser.ParseSpanByClass(body, className),
            cancellationToken);
    }

    /// <summary>지정 IO 신호의 상세 상태를 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyDictionary<string, string>>> GetIoSignalAsync(
        string network,
        string unit,
        string signal,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildIoSignalRequest(network, unit, signal),
            body => AbbRwsResponseParser.ParseObjectByClass(body, IoSignalItemClassName),
            cancellationToken);
    }

    /// <summary>서보 활성화 신호 상태를 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyDictionary<string, string>>> GetServoEnableAsync(CancellationToken cancellationToken = default) =>
        GetIoSignalAsync(DefaultIoNetwork, DefaultServoUnit, DefaultServoSignal, cancellationToken);

    /// <summary>RAPID 프로그램 실행 상태를 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyDictionary<string, string>>> GetRapidExecutionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRequest(AbbRwsAddress.Parse(RapidExecutionAddress)),
            body => AbbRwsResponseParser.ParseObjectByClass(body, RapidExecutionItemClassName),
            cancellationToken);
    }

    /// <summary>RAPID 태스크 목록을 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>> GetRapidTasksAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRequest(AbbRwsAddress.Parse(RapidTasksAddress)),
            body => AbbRwsResponseParser.ParseArrayByClass(body, RapidTaskItemClassName, int.MaxValue),
            cancellationToken);
    }

    /// <summary>컨트롤러 이벤트 로그 목록을 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>> GetEventLogsAsync(
        int maxCount = DefaultEventLogMaxCount,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (maxCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "최대 개수는 0 이상이어야 합니다.");
        }

        ThrowIfDisposed();
        EnsureOpen();
        string effectiveLanguage = language ?? _options.EventLogLanguage;
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildEventLogsRequest(effectiveLanguage),
            body => AbbRwsResponseParser.ParseArrayByClass(body, EventLogItemClassName, maxCount),
            cancellationToken);
    }

    /// <summary>컨트롤러 시스템 정보를 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyDictionary<string, string>>> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRequest(AbbRwsAddress.Parse(SystemAddress)),
            body => AbbRwsResponseParser.ParseObjectByClass(body, SystemItemClassName),
            cancellationToken);
    }

    /// <summary>RAPID 사용자 변수 실수 배열을 조회합니다.</summary>
    public Task<RobotResult<double[]>> GetRapidUserValuesAsync(
        string symbolNameOrUrl,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRapidUserValuesRequest(symbolNameOrUrl),
            body => AbbRwsResponseParser.ParseDoubleListSpanByClass(body, RapidValueClassName),
            cancellationToken);
    }
}
