using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FieldLink.OpcUa.Internal;
using FieldLink.OpcUa.Security;
using Opc.Ua;

namespace FieldLink.OpcUa;

/// <summary>OPC UA 클라이언트의 연결, 보안, 제한 시간, 동시성 설정입니다.</summary>
public sealed class OpcUaClientOptions
{
    internal const string OpcTcpScheme = "opc.tcp";
    private const string DefaultApplicationName = "FieldLink.OpcUa.Client";
    private const string DefaultApplicationUri = "urn:fieldlink:opcua:client";
    private const int DefaultConnectTimeoutSeconds = 15;
    private const int DefaultOperationTimeoutSeconds = 10;
    private const int DefaultCloseTimeoutSeconds = 5;
    private const int DefaultMaxConcurrentOperations = 8;
    private const uint DefaultSessionTimeoutMilliseconds = 60_000;
    private const int DefaultKeepAliveIntervalMilliseconds = 5_000;
    private const int DefaultMaxKeepAliveFailuresBeforeFault = 3;

    private static readonly IReadOnlyList<string> DefaultAllowedPolicies = Array.AsReadOnly(
        new[]
        {
            SecurityPolicies.Aes256_Sha256_RsaPss,
            SecurityPolicies.Aes128_Sha256_RsaOaep,
            SecurityPolicies.Basic256Sha256
        });

    private IReadOnlyList<string> _allowedSecurityPolicies = DefaultAllowedPolicies;

    /// <summary>접속할 OPC UA 서버의 <c>opc.tcp://...</c> Endpoint URL을 지정하거나 가져옵니다.</summary>
    // ValidateAndClone에서 필수 입력 null 검증을 수행한다.
    public Uri EndpointUrl { get; init; } = null!;

    /// <summary>클라이언트 애플리케이션 표시 이름을 지정하거나 가져옵니다.</summary>
    public string ApplicationName { get; init; } = DefaultApplicationName;

    /// <summary>클라이언트 X.509 인증서 SAN URI와 일치해야 하는 애플리케이션 URI를 지정하거나 가져옵니다.</summary>
    public string ApplicationUri { get; init; } = DefaultApplicationUri;

    /// <summary>요구하는 메시지 보안 모드를 지정하거나 가져옵니다. 기본값은 <see cref="MessageSecurityMode.SignAndEncrypt"/>이며 무보안 <c>None</c>으로 자동 폴백하지 않습니다.</summary>
    public MessageSecurityMode MessageSecurityMode { get; init; } = MessageSecurityMode.SignAndEncrypt;

    /// <summary>허용할 보안 정책 URI 화이트리스트를 우선순위 순으로 지정하거나 가져옵니다.</summary>
    public IReadOnlyList<string> AllowedSecurityPolicies
    {
        get => _allowedSecurityPolicies;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            _allowedSecurityPolicies = new ReadOnlyCollection<string>(value.ToArray());
        }
    }

    /// <summary>세션 활성화에 사용할 사용자 자격증명을 지정하거나 가져옵니다. 기본값은 익명입니다.</summary>
    public OpcUaUserIdentity UserIdentity { get; init; } = OpcUaUserIdentity.CreateAnonymous();

    /// <summary>인증서 저장소 및 X.509 검증 설정을 지정하거나 가져옵니다.</summary>
    // ValidateAndClone에서 필수 입력 null 검증을 수행한다.
    public OpcUaCertificateOptions Certificates { get; init; } = null!;

    /// <summary>채널과 사용자 토큰 모두 암호화되지 않은 <c>None</c> 정책일 때 사용자 이름·비밀번호 전송을 명시적으로 허용할지 여부를 지정하거나 가져옵니다.</summary>
    public bool AllowInsecureUserNameOverUnencryptedChannel { get; init; }

    /// <summary>Discovery Endpoint와 실제 접속 Endpoint URL의 호스트·포트를 연결 시 갱신할지 여부를 지정하거나 가져옵니다. 기본값은 <see langword="false"/>입니다.</summary>
    public bool UpdateEndpointUrlOnConnect { get; init; }

    /// <summary>Discovery, 보안 채널, 세션 생성 및 활성화를 포함한 전체 연결 제한 시간입니다. 기본값은 15초입니다.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(DefaultConnectTimeoutSeconds);

    /// <summary>개별 Read/Write/Browse/Subscribe/Call 서비스 호출에 적용되는 기본 전체 제한 시간입니다. 기본값은 10초입니다.</summary>
    public TimeSpan DefaultOperationTimeout { get; init; } = TimeSpan.FromSeconds(DefaultOperationTimeoutSeconds);

    /// <summary>정상 종료(<see cref="IOpcUaClient.CloseAsync"/>) 시 서버 구독 삭제 및 세션 종료에 사용할 제한 시간입니다. 기본값은 5초입니다.</summary>
    public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(DefaultCloseTimeoutSeconds);

    /// <summary>단일 세션에서 동시에 실행할 수 있는 최대 서비스 호출 수입니다. 기본값은 8입니다.</summary>
    public int MaxConcurrentOperations { get; init; } = DefaultMaxConcurrentOperations;

    /// <summary>서버에 요청할 세션 타임아웃(ms)입니다. 기본값은 60,000ms입니다.</summary>
    public uint SessionTimeoutMilliseconds { get; init; } = DefaultSessionTimeoutMilliseconds;

    /// <summary>세션 KeepAlive 주기(ms)입니다. 기본값은 5,000ms입니다.</summary>
    public int KeepAliveIntervalMilliseconds { get; init; } = DefaultKeepAliveIntervalMilliseconds;

    /// <summary>연속 KeepAlive 실패 시 클라이언트를 <see cref="OpcUaClientState.Faulted"/>로 전이할 횟수 한도입니다. 기본값은 3입니다.</summary>
    public int MaxKeepAliveFailuresBeforeFault { get; init; } = DefaultMaxKeepAliveFailuresBeforeFault;

    /// <summary>비밀번호나 개인키를 제외한 진단 요약을 반환합니다.</summary>
    public override string ToString()
    {
        return $"Endpoint={EndpointUrl}, Mode={MessageSecurityMode}, ApplicationUri={ApplicationUri}, Identity={UserIdentity}";
    }

    internal static void ValidateEndpointUri(Uri? endpointUrl, string parameterName)
    {
        if (endpointUrl is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (!endpointUrl.IsAbsoluteUri ||
            !string.Equals(endpointUrl.Scheme, OpcTcpScheme, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("OPC UA Endpoint URL은 'opc.tcp://' 스킴의 절대 URI여야 합니다.", parameterName);
        }

        if (string.IsNullOrWhiteSpace(endpointUrl.Host) || endpointUrl.Port <= 0)
        {
            throw new ArgumentException("OPC UA Endpoint URL에는 유효한 호스트와 포트가 포함되어야 합니다.", parameterName);
        }
    }

    internal OpcUaClientOptions ValidateAndClone()
    {
        ValidateEndpointUri(EndpointUrl, nameof(EndpointUrl));

        if (string.IsNullOrWhiteSpace(ApplicationName))
        {
            throw new ArgumentException("애플리케이션 이름은 비어 있을 수 없습니다.", nameof(ApplicationName));
        }

        if (string.IsNullOrWhiteSpace(ApplicationUri) || !Uri.IsWellFormedUriString(ApplicationUri, UriKind.Absolute))
        {
            throw new ArgumentException("애플리케이션 URI는 유효한 절대 URI여야 합니다.", nameof(ApplicationUri));
        }

        if (MessageSecurityMode is not (MessageSecurityMode.SignAndEncrypt or MessageSecurityMode.Sign or MessageSecurityMode.None))
        {
            throw new ArgumentOutOfRangeException(nameof(MessageSecurityMode), "지원하지 않는 메시지 보안 모드입니다.");
        }

        if (_allowedSecurityPolicies.Count == 0)
        {
            throw new ArgumentException("허용 보안 정책 목록은 최소 1개 이상이어야 합니다.", nameof(AllowedSecurityPolicies));
        }

        for (int i = 0; i < _allowedSecurityPolicies.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(_allowedSecurityPolicies[i]))
            {
                throw new ArgumentException("허용 보안 정책 URI에 빈 항목을 넣을 수 없습니다.", nameof(AllowedSecurityPolicies));
            }
        }

        if (UserIdentity is null)
        {
            throw new ArgumentNullException(nameof(UserIdentity));
        }

        if (Certificates is null)
        {
            throw new ArgumentNullException(nameof(Certificates));
        }

        Certificates.Validate(MessageSecurityMode);
        ValidateTimeoutsAndLimits();

        return new OpcUaClientOptions
        {
            EndpointUrl = EndpointUrl,
            ApplicationName = ApplicationName,
            ApplicationUri = ApplicationUri,
            MessageSecurityMode = MessageSecurityMode,
            AllowedSecurityPolicies = _allowedSecurityPolicies.ToArray(),
            UserIdentity = UserIdentity.Clone(),
            Certificates = Certificates.Clone(),
            AllowInsecureUserNameOverUnencryptedChannel = AllowInsecureUserNameOverUnencryptedChannel,
            UpdateEndpointUrlOnConnect = UpdateEndpointUrlOnConnect,
            ConnectTimeout = ConnectTimeout,
            DefaultOperationTimeout = DefaultOperationTimeout,
            CloseTimeout = CloseTimeout,
            MaxConcurrentOperations = MaxConcurrentOperations,
            SessionTimeoutMilliseconds = SessionTimeoutMilliseconds,
            KeepAliveIntervalMilliseconds = KeepAliveIntervalMilliseconds,
            MaxKeepAliveFailuresBeforeFault = MaxKeepAliveFailuresBeforeFault
        };
    }

    private void ValidateTimeoutsAndLimits()
    {
        OpcUaDeadline.ValidatePositiveTimeout(
            ConnectTimeout,
            nameof(ConnectTimeout),
            "연결 제한 시간은 0보다 크고 int.MaxValue 밀리초 이하이어야 합니다.");
        OpcUaDeadline.ValidatePositiveTimeout(
            DefaultOperationTimeout,
            nameof(DefaultOperationTimeout),
            "기본 작업 제한 시간은 0보다 크고 int.MaxValue 밀리초 이하이어야 합니다.");
        OpcUaDeadline.ValidatePositiveTimeout(
            CloseTimeout,
            nameof(CloseTimeout),
            "종료 제한 시간은 0보다 크고 int.MaxValue 밀리초 이하이어야 합니다.");

        if (MaxConcurrentOperations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrentOperations), "최대 동시 작업 수는 1 이상이어야 합니다.");
        }

        if (SessionTimeoutMilliseconds == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SessionTimeoutMilliseconds), "세션 타임아웃은 0보다 커야 합니다.");
        }

        if (KeepAliveIntervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(KeepAliveIntervalMilliseconds), "KeepAlive 주기는 0보다 커야 합니다.");
        }

        if (MaxKeepAliveFailuresBeforeFault < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxKeepAliveFailuresBeforeFault), "KeepAlive 실패 한도는 1 이상이어야 합니다.");
        }
    }
}
