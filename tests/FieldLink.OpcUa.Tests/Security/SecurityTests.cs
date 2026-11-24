using System;
using System.IO;
using System.Threading.Tasks;
using FieldLink.OpcUa.Discovery;
using FieldLink.OpcUa.Tests.Fixtures;
using Opc.Ua;
using Xunit;

namespace FieldLink.OpcUa.Tests.Security;

public sealed class SecurityTests
{
    [Fact]
    public async Task S01_SignAndEncryptWithTrustedCertificates_ConnectsSuccessfully()
    {
        using OpcUaTestServer server = new(new OpcUaTestServerOptions
        {
            IncludeSignAndEncryptEndpoint = true,
            IncludeNoneEndpoint = false,
            AutoAcceptClientCertificates = false
        });
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server, trustClientOnServer: true);

        OpcUaClientOptions options = clientSetup.CreateClientOptions(server.EndpointUrl);
        using OpcUaClient client = new(options);

        await client.OpenAsync();
        Assert.Equal(OpcUaClientState.Open, client.State);

        await client.CloseAsync();
        Assert.Equal(OpcUaClientState.Closed, client.State);
    }

    [Fact]
    public async Task S02_ServerExposesOnlyNoneEndpoint_ClientFailsWithoutFallback()
    {
        using OpcUaTestServer server = new(new OpcUaTestServerOptions
        {
            IncludeSignAndEncryptEndpoint = false,
            IncludeNoneEndpoint = true,
            AutoAcceptClientCertificates = true
        });
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.ProvisionClientCertificateAsync();

        OpcUaClientOptions options = clientSetup.CreateClientOptions(server.EndpointUrl);
        using OpcUaClient client = new(options);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync());
        Assert.Contains("None", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OpcUaClientState.Closed, client.State);
    }

    [Fact]
    public async Task S02_SelectEndpoint_RejectsUnencryptedUserNameTokenOverNoneChannelUnlessOptedIn()
    {
        EndpointDescription[] endpoints =
        {
            new EndpointDescription
            {
                EndpointUrl = "opc.tcp://127.0.0.1:4840",
                TransportProfileUri = Profiles.UaTcpTransport,
                SecurityMode = MessageSecurityMode.None,
                SecurityPolicyUri = SecurityPolicies.None,
                SecurityLevel = 1,
                UserIdentityTokens = new UserTokenPolicyCollection
                {
                    new UserTokenPolicy(UserTokenType.UserName)
                    {
                        SecurityPolicyUri = SecurityPolicies.None
                    }
                }
            }
        };

        OpcUaClientOptions blockedOptions = new()
        {
            EndpointUrl = new Uri("opc.tcp://127.0.0.1:4840"),
            MessageSecurityMode = MessageSecurityMode.None,
            AllowedSecurityPolicies = new[] { SecurityPolicies.None },
            UserIdentity = OpcUaUserIdentity.CreateUserName("operator", "SecretPass!"),
            Certificates = new OpcUa.Security.OpcUaCertificateOptions
            {
                ApplicationCertificateStorePath = "own",
                TrustedPeerCertificatesStorePath = "trusted",
                TrustedIssuerCertificatesStorePath = "issuer",
                RejectedCertificateStorePath = "rejected"
            }
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            OpcUaDiscovery.SelectEndpoint(endpoints, blockedOptions));
        Assert.DoesNotContain("SecretPass!", ex.Message, StringComparison.Ordinal);

        OpcUaClientOptions allowedOptions = new()
        {
            EndpointUrl = blockedOptions.EndpointUrl,
            MessageSecurityMode = MessageSecurityMode.None,
            AllowedSecurityPolicies = new[] { SecurityPolicies.None },
            UserIdentity = blockedOptions.UserIdentity,
            Certificates = blockedOptions.Certificates,
            AllowInsecureUserNameOverUnencryptedChannel = true
        };

        ConfiguredEndpoint selected = OpcUaDiscovery.SelectEndpoint(endpoints, allowedOptions);
        Assert.Equal(MessageSecurityMode.None, selected.Description.SecurityMode);
    }

    [Fact]
    public async Task S03_UntrustedServerCertificate_PreservesBadCertificateUntrustedAndWritesRejectedStore()
    {
        using OpcUaTestServer server = new(new OpcUaTestServerOptions
        {
            IncludeSignAndEncryptEndpoint = true,
            AutoAcceptClientCertificates = true
        });
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.ProvisionClientCertificateAsync();

        OpcUaClientOptions options = clientSetup.CreateClientOptions(server.EndpointUrl);
        using OpcUaClient client = new(options);

        ServiceResultException exception = await Assert.ThrowsAsync<ServiceResultException>(() => client.OpenAsync());
        Assert.Equal(StatusCodes.BadCertificateUntrusted, exception.StatusCode);

        // SDK CertificateValidator는 Rejected 저장소 기록을 백그라운드 Task로 수행하므로 짧은 대기 후 확인한다.
        string[] rejectedFiles = Array.Empty<string>();
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (rejectedFiles.Length == 0 && DateTime.UtcNow < deadline)
        {
            rejectedFiles = Directory.GetFiles(clientSetup.RejectedPath, "*.*", SearchOption.AllDirectories);
            if (rejectedFiles.Length == 0)
            {
                await Task.Delay(20);
            }
        }

        Assert.NotEmpty(rejectedFiles);
    }

    [Fact]
    public async Task S04_ExpiredServerCertificate_FailsWithBadCertificateTimeInvalid()
    {
        using OpcUaTestServer server = new(new OpcUaTestServerOptions
        {
            IncludeSignAndEncryptEndpoint = true,
            AutoAcceptClientCertificates = true,
            UseExpiredServerCertificate = true
        });
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server, trustClientOnServer: false);

        OpcUaClientOptions options = clientSetup.CreateClientOptions(server.EndpointUrl);
        using OpcUaClient client = new(options);

        ServiceResultException exception = await Assert.ThrowsAsync<ServiceResultException>(() => client.OpenAsync());
        Assert.Equal(StatusCodes.BadCertificateTimeInvalid, exception.StatusCode);
    }

    [Fact]
    public async Task S05_HostnameMismatchInServerCertificate_PreservesBadCertificateHostNameInvalid()
    {
        using OpcUaTestServer server = new(new OpcUaTestServerOptions
        {
            IncludeSignAndEncryptEndpoint = true,
            AutoAcceptClientCertificates = true,
            OverrideServerCertificateHost = "other.host.invalid"
        });
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server, trustClientOnServer: false);

        OpcUaClientOptions options = clientSetup.CreateClientOptions(server.EndpointUrl);
        using OpcUaClient client = new(options);

        ServiceResultException exception = await Assert.ThrowsAsync<ServiceResultException>(() => client.OpenAsync());
        Assert.Equal(StatusCodes.BadCertificateHostNameInvalid, exception.StatusCode);
    }

    [Fact]
    public async Task S05_ApplicationUriMismatchInServerCertificate_PreservesBadCertificateUriInvalid()
    {
        using OpcUaTestServer server = new(new OpcUaTestServerOptions
        {
            IncludeSignAndEncryptEndpoint = true,
            AutoAcceptClientCertificates = true,
            OverrideServerCertificateAppUri = "urn:mismatched:opcua:server"
        });
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server, trustClientOnServer: false);

        OpcUaClientOptions options = clientSetup.CreateClientOptions(server.EndpointUrl);
        using OpcUaClient client = new(options);

        ServiceResultException exception = await Assert.ThrowsAsync<ServiceResultException>(() => client.OpenAsync());
        Assert.Equal(StatusCodes.BadCertificateUriInvalid, exception.StatusCode);
    }

    [Fact]
    public async Task S06_MissingClientOwnCertificate_FailsWithoutAutoGeneration()
    {
        using OpcUaTestServer server = new();
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        OpcUaClientOptions options = clientSetup.CreateClientOptions(server.EndpointUrl);
        using OpcUaClient client = new(options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.OpenAsync());
        string[] ownFiles = Directory.GetFiles(clientSetup.OwnPath, "*.*", SearchOption.AllDirectories);
        Assert.Empty(ownFiles);
    }

    [Fact]
    public async Task S07_UserNameAuthentication_ValidSucceedsAndInvalidRedactsPassword()
    {
        const string ValidUser = "engineer";
        const string ValidPassword = "CorrectHorseBattery99!";
        const string WrongPassword = "WrongSecretPassword!42";

        using OpcUaTestServer server = new(new OpcUaTestServerOptions
        {
            IncludeSignAndEncryptEndpoint = true,
            AllowAnonymous = false,
            Username = ValidUser,
            Password = ValidPassword
        });
        await server.StartServerAsync();

        using TestClientCertificateSetup clientSetup = new(server.RootDirectory);
        await clientSetup.TrustServerAsync(server, trustClientOnServer: true);

        OpcUaClientOptions badOptions = clientSetup.CreateClientOptions(
            server.EndpointUrl,
            OpcUaUserIdentity.CreateUserName(ValidUser, WrongPassword));

        Assert.DoesNotContain(WrongPassword, badOptions.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(WrongPassword, badOptions.UserIdentity.ToString(), StringComparison.Ordinal);

        using OpcUaClient badClient = new(badOptions);
        ServiceResultException authException = await Assert.ThrowsAsync<ServiceResultException>(() => badClient.OpenAsync());
        Assert.Equal(StatusCodes.BadUserAccessDenied, authException.StatusCode);
        Assert.DoesNotContain(WrongPassword, authException.ToString(), StringComparison.Ordinal);
        Assert.Equal(OpcUaClientState.Closed, badClient.State);

        OpcUaClientOptions validOptions = clientSetup.CreateClientOptions(
            server.EndpointUrl,
            OpcUaUserIdentity.CreateUserName(ValidUser, ValidPassword));
        using OpcUaClient validClient = new(validOptions);
        await validClient.OpenAsync();
        Assert.Equal(OpcUaClientState.Open, validClient.State);
        await validClient.CloseAsync();
    }
}
