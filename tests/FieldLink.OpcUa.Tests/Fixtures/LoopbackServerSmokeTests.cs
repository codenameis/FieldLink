using System;
using System.IO;
using System.Threading.Tasks;
using Opc.Ua;
using Xunit;

namespace FieldLink.OpcUa.Tests.Fixtures;

public sealed class LoopbackServerSmokeTests
{
    [Fact]
    public async Task OpcUaTestServer_StartsServesDiscoveryAndCleansUpTempDirectory()
    {
        string tempRoot;
        using (OpcUaTestServer server = new(new OpcUaTestServerOptions { IncludeSignAndEncryptEndpoint = true, IncludeNoneEndpoint = false }))
        {
            tempRoot = server.RootDirectory;
            await server.StartServerAsync();

            using DiscoveryClient discoveryClient = await DiscoveryClient.CreateAsync(
                server.EndpointUrl,
                EndpointConfiguration.Create(),
                DefaultTelemetry.Create(static _ => { }),
                DiagnosticsMasks.None,
                default);
            EndpointDescriptionCollection endpoints = await discoveryClient.GetEndpointsAsync(null, default);

            Assert.NotEmpty(endpoints);
            Assert.All(endpoints, endpoint =>
                Assert.Equal(MessageSecurityMode.SignAndEncrypt, endpoint.SecurityMode));
            Assert.Contains(endpoints, endpoint =>
                string.Equals(endpoint.SecurityPolicyUri, SecurityPolicies.Basic256Sha256, StringComparison.Ordinal));
        }

        Assert.False(Directory.Exists(tempRoot));
    }

    [Fact]
    public async Task OpcUaTestServer_CanRestartNewInstanceOnCleanPort()
    {
        using OpcUaTestServer first = new();
        await first.StartServerAsync();
        Uri firstUri = first.EndpointUrl;
        await first.StopAsync();

        using OpcUaTestServer second = new();
        await second.StartServerAsync();
        Assert.NotEqual(0, second.EndpointUrl.Port);
        Assert.NotEqual(firstUri.Port, second.EndpointUrl.Port);
        await second.StopAsync();
    }
}
