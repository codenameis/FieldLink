using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;

namespace FieldLink.OpcUa.Transport;

internal sealed class OpcUaSdkDiscoveryClient : IOpcUaDiscoveryClient
{
    private const int DiscoveryCloseTimeoutSeconds = 2;

    private static readonly TimeSpan DiscoveryCloseTimeout = TimeSpan.FromSeconds(DiscoveryCloseTimeoutSeconds);

    public async Task<IReadOnlyList<EndpointDescription>> GetEndpointsAsync(
        Uri endpointUrl,
        CancellationToken cancellationToken)
    {
        EndpointConfiguration endpointConfiguration = EndpointConfiguration.Create();
        ITelemetryContext telemetry = DefaultTelemetry.Create(static _ => { });
        using DiscoveryClient client = await DiscoveryClient.CreateAsync(
            endpointUrl,
            endpointConfiguration,
            telemetry,
            DiagnosticsMasks.None,
            cancellationToken).ConfigureAwait(false);

        try
        {
            EndpointDescriptionCollection endpoints = await client
                .GetEndpointsAsync(null, cancellationToken)
                .ConfigureAwait(false);
            return endpoints.ToArray();
        }
        finally
        {
            await CloseQuietlyAsync(client).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
    }

    private static async Task CloseQuietlyAsync(DiscoveryClient client)
    {
        try
        {
            using CancellationTokenSource closeTimeout = new(DiscoveryCloseTimeout);
            await client.CloseAsync(closeTimeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Discovery 연결 정리 중 타임아웃은 무시
        }
        catch (ServiceResultException)
        {
            // Discovery 연결이 이미 닫힌 경우 무시
        }
        catch (ObjectDisposedException)
        {
            // Discovery 클라이언트가 이미 폐기된 경우 무시
        }
    }
}
