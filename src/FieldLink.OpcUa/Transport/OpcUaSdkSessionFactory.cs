using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;

namespace FieldLink.OpcUa.Transport;

internal sealed class OpcUaSdkSessionFactory : IOpcUaSessionFactory
{
    public async Task<IOpcUaSessionAdapter> CreateSessionAsync(
        ApplicationConfiguration configuration,
        ConfiguredEndpoint endpoint,
        OpcUaClientOptions options,
        CancellationToken cancellationToken)
    {
        string? selectedPolicyId = endpoint.Description.UserIdentityTokens is { Count: > 0 } tokens
            ? tokens[0]?.PolicyId
            : null;

        // 세션 수명 규칙: SDK 자동 재접속 핸들러(SessionReconnectHandler)는 절대 사용하지 않으며 checkDomain을 강제한다.
        DefaultSessionFactory sessionFactory = new(DefaultTelemetry.Create(static _ => { }));
        ISession session = await sessionFactory.CreateAsync(
            configuration,
            endpoint,
            options.UpdateEndpointUrlOnConnect,
            checkDomain: true,
            options.ApplicationName,
            options.SessionTimeoutMilliseconds,
            options.UserIdentity.ToSdkIdentity(selectedPolicyId),
            preferredLocales: null,
            cancellationToken).ConfigureAwait(false);

        session.KeepAliveInterval = options.KeepAliveIntervalMilliseconds;
        return new OpcUaSdkSessionAdapter(session);
    }
}
