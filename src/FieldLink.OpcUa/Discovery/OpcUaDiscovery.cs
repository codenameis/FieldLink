using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Internal;
using FieldLink.OpcUa.Transport;
using Opc.Ua;

namespace FieldLink.OpcUa.Discovery;

/// <summary>OPC UA 서버의 Endpoint 목록을 조회하고 보안 정책에 따라 결정적으로 선택하는 기능을 제공합니다.</summary>
public static class OpcUaDiscovery
{
    private const int DefaultDiscoveryTimeoutSeconds = 10;

    private static readonly TimeSpan DefaultDiscoveryTimeout = TimeSpan.FromSeconds(DefaultDiscoveryTimeoutSeconds);

    /// <summary>지정한 OPC UA 서버의 <c>GetEndpoints</c> 서비스를 호출하여 필터링되지 않은 전체 Endpoint 목록을 반환합니다.</summary>
    /// <param name="endpointUrl"><c>opc.tcp://...</c> 형식의 서버 Discovery Endpoint URL입니다.</param>
    /// <param name="timeout">조회 전체 제한 시간입니다. <see langword="null"/>이면 기본 10초를 적용합니다.</param>
    /// <param name="cancellationToken">호출자 취소 토큰입니다.</param>
    /// <returns>서버가 광고하는 전체 <see cref="EndpointDescription"/> 목록입니다.</returns>
    public static async Task<IReadOnlyList<EndpointDescription>> GetEndpointsAsync(
        Uri endpointUrl,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using OpcUaSdkDiscoveryClient discoveryClient = new();
        return await GetEndpointsAsync(endpointUrl, discoveryClient, timeout, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<IReadOnlyList<EndpointDescription>> GetEndpointsAsync(
        Uri endpointUrl,
        IOpcUaDiscoveryClient discoveryClient,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        OpcUaClientOptions.ValidateEndpointUri(endpointUrl, nameof(endpointUrl));
        TimeSpan effectiveTimeout = timeout ?? DefaultDiscoveryTimeout;
        OpcUaDeadline deadline = OpcUaDeadline.StartAfter(effectiveTimeout);

        return await deadline.RunAsync(
            discoveryToken => discoveryClient.GetEndpointsAsync(endpointUrl, discoveryToken),
            nameof(GetEndpointsAsync),
            cancellationToken).ConfigureAwait(false);
    }

    internal static ConfiguredEndpoint SelectEndpoint(
        IReadOnlyList<EndpointDescription> endpoints,
        OpcUaClientOptions options,
        ApplicationConfiguration? configuration = null)
    {
        if (endpoints is null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        EndpointDescription? bestEndpoint = null;
        int bestPolicyRank = int.MaxValue;
        UserTokenPolicyCollection? bestApprovedTokens = null;

        for (int i = 0; i < endpoints.Count; i++)
        {
            EndpointDescription? candidate = endpoints[i];
            if (candidate is null ||
                !OpcUaEndpointPolicyFilter.IsCompatibleEndpoint(candidate, options, out int policyRank, out UserTokenPolicyCollection? approvedTokens))
            {
                continue;
            }

            if (bestEndpoint is null ||
                policyRank < bestPolicyRank ||
                (policyRank == bestPolicyRank && candidate.SecurityLevel > bestEndpoint.SecurityLevel))
            {
                bestEndpoint = candidate;
                bestPolicyRank = policyRank;
                bestApprovedTokens = approvedTokens;
            }
        }

        if (bestEndpoint is null || bestApprovedTokens is null)
        {
            throw new InvalidOperationException(
                $"보안 요구사항(SecurityMode={options.MessageSecurityMode}, Identity={options.UserIdentity}, " +
                $"AllowedPolicies=[{string.Join(", ", options.AllowedSecurityPolicies)}])을 만족하는 Endpoint를 찾지 못했습니다. " +
                $"수신된 후보 {endpoints.Count}건 중 무보안 None으로 자동 폴백하지 않고 연결을 중단합니다.");
        }

        EndpointDescription selectedEndpoint = (EndpointDescription)bestEndpoint.Clone();
        selectedEndpoint.UserIdentityTokens = bestApprovedTokens;

        EndpointConfiguration endpointConfiguration = configuration is not null
            ? EndpointConfiguration.Create(configuration)
            : EndpointConfiguration.Create();
        return new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration)
        {
            UpdateBeforeConnect = options.UpdateEndpointUrlOnConnect
        };
    }
}
