using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FieldLink.OpcUa.Security;
using Opc.Ua;

namespace FieldLink.OpcUa.Tests.Fixtures;

internal sealed class TestClientCertificateSetup : IDisposable
{
    public TestClientCertificateSetup(string rootDirectory)
    {
        BaseDirectory = Path.Combine(rootDirectory, "client", Guid.NewGuid().ToString("N"));
        OwnPath = Path.Combine(BaseDirectory, "own");
        TrustedPath = Path.Combine(BaseDirectory, "trusted");
        IssuerPath = Path.Combine(BaseDirectory, "issuer");
        RejectedPath = Path.Combine(BaseDirectory, "rejected");

        Directory.CreateDirectory(OwnPath);
        Directory.CreateDirectory(TrustedPath);
        Directory.CreateDirectory(IssuerPath);
        Directory.CreateDirectory(RejectedPath);
    }

    public string BaseDirectory { get; }

    public string OwnPath { get; }

    public string TrustedPath { get; }

    public string IssuerPath { get; }

    public string RejectedPath { get; }

    public string ApplicationUri { get; } = "urn:fieldlink:opcua:testclient";

    public string ApplicationName { get; } = "FieldLink TestClient";

    public X509Certificate2? ClientCertificate { get; private set; }

    public OpcUaCertificateOptions ToCertificateOptions()
    {
        return new OpcUaCertificateOptions
        {
            StoreType = OpcUaCertificateStoreType.Directory,
            ApplicationCertificateStorePath = OwnPath,
            TrustedPeerCertificatesStorePath = TrustedPath,
            TrustedIssuerCertificatesStorePath = IssuerPath,
            RejectedCertificateStorePath = RejectedPath
        };
    }

    public OpcUaClientOptions CreateClientOptions(
        Uri endpointUrl,
        OpcUaUserIdentity? identity = null,
        MessageSecurityMode securityMode = MessageSecurityMode.SignAndEncrypt,
        int maxConcurrentOperations = 8,
        TimeSpan? connectTimeout = null,
        TimeSpan? operationTimeout = null)
    {
        return new OpcUaClientOptions
        {
            EndpointUrl = endpointUrl,
            ApplicationName = ApplicationName,
            ApplicationUri = ApplicationUri,
            MessageSecurityMode = securityMode,
            UserIdentity = identity ?? OpcUaUserIdentity.CreateAnonymous(),
            Certificates = ToCertificateOptions(),
            MaxConcurrentOperations = maxConcurrentOperations,
            ConnectTimeout = connectTimeout ?? TimeSpan.FromSeconds(15),
            DefaultOperationTimeout = operationTimeout ?? TimeSpan.FromSeconds(10)
        };
    }

    public async Task ProvisionClientCertificateAsync()
    {
        ClientCertificate = await OpcUaTestServer.ProvisionCertificateAsync(
            OwnPath,
            ApplicationUri,
            ApplicationName);
    }

    public async Task TrustServerAsync(OpcUaTestServer server, bool trustClientOnServer = true)
    {
        if (ClientCertificate is null)
        {
            await ProvisionClientCertificateAsync();
        }

        using X509Certificate2 serverCert = server.ServerPublicCertificate;
        await OpcUaTestServer.TrustCertificateAsync(TrustedPath, serverCert);

        if (trustClientOnServer)
        {
            await server.TrustClientCertificateAsync(ClientCertificate!);
        }
    }

    public void Dispose()
    {
        ClientCertificate?.Dispose();
    }
}
