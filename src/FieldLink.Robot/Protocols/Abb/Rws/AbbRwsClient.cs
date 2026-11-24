using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Robot.Abstractions;
using FieldLink.Robot.Conversion;
using FieldLink.Transport.Http;

namespace FieldLink.Robot.Protocols.Abb.Rws;

/// <summary>ABB Robot Web Services(RWS) HTTP/HTTPS REST API 클라이언트입니다.</summary>
public sealed class AbbRwsClient : IRobotClient
{
    private const int HttpStatusUnauthorized = 401;

    private readonly IHttpTransport _transport;
    private readonly AbbRwsOptions _options;
    private bool _isOpen;
    private bool _isDisposed;

    /// <summary>설정 정보로부터 내부 HTTP 전송을 생성해 ABB RWS 클라이언트를 초기화합니다.</summary>
    /// <param name="options">ABB RWS 연결 및 인증 설정입니다.</param>
    public AbbRwsClient(AbbRwsOptions options)
        : this(new HttpTransport((options ?? throw new ArgumentNullException(nameof(options))).ToHttpTransportOptions()), options)
    {
    }

    /// <summary>사용자 정의 HTTP 전송을 주입받아 ABB RWS 클라이언트를 초기화합니다.</summary>
    /// <param name="transport">HTTP 요청을 수행할 전송 구현체입니다.</param>
    /// <param name="options">디코딩 및 언어 설정입니다. null이면 기본 설정을 사용합니다.</param>
    public AbbRwsClient(IHttpTransport transport, AbbRwsOptions? options = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? new AbbRwsOptions();
    }

    /// <inheritdoc />
    public bool IsOpen => !_isDisposed && _isOpen;

    /// <inheritdoc />
    public Task<RobotResult> OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_isOpen)
        {
            throw new InvalidOperationException("ABB RWS 클라이언트가 이미 열려 있습니다.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        _isOpen = true;
        return Task.FromResult(RobotResult.Success());
    }

    /// <inheritdoc />
    public Task<RobotResult> CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isOpen = false;
        return Task.FromResult(RobotResult.Success());
    }

    /// <inheritdoc />
    public async Task<RobotResult<byte[]>> ReadRawAsync(string address, CancellationToken cancellationToken = default)
    {
        RobotResult<string> stringResult = await ReadStringAsync(address, cancellationToken).ConfigureAwait(false);
        return stringResult.IsSuccess
            ? RobotResult.Success(Encoding.UTF8.GetBytes(stringResult.Value))
            : RobotResult.Failure<byte[]>(stringResult.Error);
    }

    /// <inheritdoc />
    public Task<RobotResult<string>> ReadStringAsync(string address, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        AbbRwsAddress parsed = AbbRwsAddress.Parse(address);

        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRequest(parsed, _options.EventLogLanguage),
            body => AbbRwsResponseParser.ParseShortcutAsString(parsed, body),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteRawAsync(string address, byte[] data, CancellationToken cancellationToken = default)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        ThrowIfDisposed();
        EnsureOpen();
        AbbRwsAddress parsed = AbbRwsAddress.Parse(address);
        if (!parsed.IsRawUrl)
        {
            return Task.FromResult(RobotResult.Failure(
                new RobotError(RobotErrorKind.Unsupported, "ABB RWS 단축 주소에는 쓰기를 지원하지 않습니다. 'url=' 접두사를 사용하세요.")));
        }

        return HttpPostBytesAsync(parsed.RawUrl!, data, customHeaders: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<RobotResult> WriteStringAsync(string address, string value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return WriteRawAsync(address, Encoding.UTF8.GetBytes(value), cancellationToken);
    }

    /// <summary>원시 URL에 대해 HTTP GET 요청을 실행하고 응답 본문 문자열을 반환합니다.</summary>
    public Task<RobotResult<string>> HttpGetAsync(string rawUrl, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(AbbRwsRequestBuilder.BuildRawGetRequest(rawUrl), body => body, cancellationToken);
    }

    /// <summary>원시 URL에 대해 HTTP POST 요청을 실행하고 응답 본문 문자열을 반환합니다.</summary>
    public Task<RobotResult<string>> HttpPostAsync(
        string rawUrl,
        string body,
        IReadOnlyDictionary<string, string>? customHeaders = null,
        CancellationToken cancellationToken = default)
    {
        if (body is null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        ThrowIfDisposed();
        EnsureOpen();
        HttpTransportRequest request = AbbRwsRequestBuilder.BuildRawPostRequest(
            rawUrl,
            Encoding.UTF8.GetBytes(body),
            _options.DefaultContentType,
            customHeaders);

        return ExecuteRequestAsync(request, responseBody => responseBody, cancellationToken);
    }

    /// <summary>컨트롤러 상태(<c>CtrlState</c>)를 조회합니다.</summary>
    public Task<RobotResult<string>> GetCtrlStateAsync(CancellationToken cancellationToken = default) =>
        ReadStringAsync("CtrlState", cancellationToken);

    /// <summary>모션 시스템 오류 상태(<c>ErrorState</c>)를 조회합니다.</summary>
    public Task<RobotResult<string>> GetErrorStateAsync(CancellationToken cancellationToken = default) =>
        ReadStringAsync("ErrorState", cancellationToken);

    /// <summary>지정 기구 단위의 관절 목표 각도를 조회합니다.</summary>
    public Task<RobotResult<double[]>> GetJointTargetAsync(string mechunit = "ROB_1", CancellationToken cancellationToken = default)
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
        string mechunit = "ROB_1",
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRobotTargetRequest(mechunit),
            body => AbbRwsResponseParser.ParseObjectByClass(body, "ms-robtargets"),
            cancellationToken);
    }

    /// <summary>속도 비율(%)을 조회합니다.</summary>
    public Task<RobotResult<string>> GetSpeedRatioAsync(CancellationToken cancellationToken = default) =>
        ReadStringAsync("SpeedRatio", cancellationToken);

    /// <summary>운전 모드를 조회합니다.</summary>
    public Task<RobotResult<string>> GetOperationModeAsync(CancellationToken cancellationToken = default) =>
        ReadStringAsync("OperationMode", cancellationToken);

    /// <summary>지정 IO 장치의 입력 또는 출력 값을 조회합니다.</summary>
    public Task<RobotResult<string>> GetDeviceIoAsync(
        string deviceName,
        bool isOutput = false,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        string className = isOutput ? "outdata" : "indata";
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
            body => AbbRwsResponseParser.ParseObjectByClass(body, "ios-signal"),
            cancellationToken);
    }

    /// <summary>서보 활성화 신호 상태를 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyDictionary<string, string>>> GetServoEnableAsync(CancellationToken cancellationToken = default) =>
        GetIoSignalAsync("Local", "DRV_1", "DRV1K1", cancellationToken);

    /// <summary>RAPID 프로그램 실행 상태를 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyDictionary<string, string>>> GetRapidExecutionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRequest(AbbRwsAddress.Parse("RapidExecution")),
            body => AbbRwsResponseParser.ParseObjectByClass(body, "rap-execution"),
            cancellationToken);
    }

    /// <summary>RAPID 태스크 목록을 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>> GetRapidTasksAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRequest(AbbRwsAddress.Parse("RapidTasks")),
            body => AbbRwsResponseParser.ParseArrayByClass(body, "rap-task-li", int.MaxValue),
            cancellationToken);
    }

    /// <summary>컨트롤러 이벤트 로그 목록을 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>> GetEventLogsAsync(
        int maxCount = 10,
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
            body => AbbRwsResponseParser.ParseArrayByClass(body, "elog-message-li", maxCount),
            cancellationToken);
    }

    /// <summary>컨트롤러 시스템 정보를 조회합니다.</summary>
    public Task<RobotResult<IReadOnlyDictionary<string, string>>> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureOpen();
        return ExecuteRequestAsync(
            AbbRwsRequestBuilder.BuildRequest(AbbRwsAddress.Parse("system")),
            body => AbbRwsResponseParser.ParseObjectByClass(body, "sys-system-li"),
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
            body => AbbRwsResponseParser.ParseDoubleListSpanByClass(body, "value"),
            cancellationToken);
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

    private Task<RobotResult> HttpPostBytesAsync(
        string rawUrl,
        byte[] data,
        IReadOnlyDictionary<string, string>? customHeaders,
        CancellationToken cancellationToken)
    {
        HttpTransportRequest request = AbbRwsRequestBuilder.BuildRawPostRequest(
            rawUrl,
            data,
            _options.DefaultContentType,
            customHeaders);

        return RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                HttpTransportResponse response = await _transport.SendAsync(request, token).ConfigureAwait(false);
                EnsureSuccessStatusCode(response);
            },
            _options.Timeout,
            cancellationToken);
    }

    private Task<RobotResult<T>> ExecuteRequestAsync<T>(
        HttpTransportRequest request,
        Func<string, T> parser,
        CancellationToken cancellationToken) =>
        RobotErrorMapper.ExecuteAsync(
            async token =>
            {
                HttpTransportResponse response = await _transport.SendAsync(request, token).ConfigureAwait(false);
                EnsureSuccessStatusCode(response);
                string body = AbbRwsResponseParser.DecodeBody(response.GetBodyBytes(), _options.UseIso88591);
                return parser(body);
            },
            _options.Timeout,
            cancellationToken);

    private static void EnsureSuccessStatusCode(HttpTransportResponse response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        byte[] rawBody = response.GetBodyBytes();
        if (response.StatusCode == HttpStatusUnauthorized)
        {
            throw new RobotException(
                RobotErrorKind.AuthenticationFailed,
                "ABB RWS HTTP 인증에 실패했습니다. (401 Unauthorized)",
                rawBody)
            {
                ProtocolErrorCode = HttpStatusUnauthorized
            };
        }

        throw new RobotDeviceException(
            response.StatusCode,
            $"ABB RWS HTTP 요청이 실패했습니다. ({response.StatusCode} {response.ReasonPhrase})",
            rawBody);
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("ABB RWS 클라이언트가 열려 있지 않습니다. OpenAsync를 먼저 호출하세요.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AbbRwsClient));
        }
    }
}
