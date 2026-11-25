using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.OpcUa.Discovery;
using FieldLink.OpcUa.Security;
using FieldLink.OpcUa.Tests.Fixtures;
using Opc.Ua;
using Xunit;

namespace FieldLink.OpcUa.Tests.Security;

public sealed class SecurityTests
{
    [Fact]
    public async Task S01_SignAndEncryptWithTrustedCertificates_ConnectsSuccessfully()
    {
        await using OpcUaTestServer server = new(new OpcUaTestServerOptions
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
        await using OpcUaTestServer server = new(new OpcUaTestServerOptions
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
        await using OpcUaTestServer server = new(new OpcUaTestServerOptions
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
        await using OpcUaTestServer server = new(new OpcUaTestServerOptions
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
        await using OpcUaTestServer server = new(new OpcUaTestServerOptions
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
        await using OpcUaTestServer server = new(new OpcUaTestServerOptions
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
        await using OpcUaTestServer server = new();
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

        await using OpcUaTestServer server = new(new OpcUaTestServerOptions
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

    [Fact]
    public void S07_SelectEndpoint_RejectsMismatchedSecurityModeAndSecurityPolicyUri()
    {
        EndpointDescription mismatchedNoneMode = new()
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840",
            TransportProfileUri = Profiles.UaTcpTransport,
            SecurityMode = MessageSecurityMode.None,
            SecurityPolicyUri = SecurityPolicies.Basic256Sha256,
            UserIdentityTokens = new UserTokenPolicyCollection
            {
                new UserTokenPolicy { PolicyId = "anon", TokenType = UserTokenType.Anonymous }
            }
        };

        OpcUaClientOptions noneOptions = CreateSecureEndpointTestOptions(
            MessageSecurityMode.None,
            new[] { SecurityPolicies.None },
            OpcUaUserIdentity.CreateAnonymous());

        Assert.Throws<InvalidOperationException>(() =>
            OpcUaDiscovery.SelectEndpoint(new[] { mismatchedNoneMode }, noneOptions));

        EndpointDescription mismatchedEncryptedMode = new()
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840",
            TransportProfileUri = Profiles.UaTcpTransport,
            SecurityMode = MessageSecurityMode.SignAndEncrypt,
            SecurityPolicyUri = SecurityPolicies.None,
            UserIdentityTokens = new UserTokenPolicyCollection
            {
                new UserTokenPolicy { PolicyId = "anon", TokenType = UserTokenType.Anonymous }
            }
        };

        OpcUaClientOptions encryptedOptions = CreateSecureEndpointTestOptions(
            MessageSecurityMode.SignAndEncrypt,
            new[] { SecurityPolicies.Basic256Sha256 },
            OpcUaUserIdentity.CreateAnonymous());

        Assert.Throws<InvalidOperationException>(() =>
            OpcUaDiscovery.SelectEndpoint(new[] { mismatchedEncryptedMode }, encryptedOptions));
    }

    [Fact]
    public void S08_SelectEndpoint_FiltersPlaintextAndSha1UserNameTokensWithoutMutatingInputAndBindsPolicyId()
    {
        EndpointDescription serverEndpoint = new()
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840",
            TransportProfileUri = Profiles.UaTcpTransport,
            SecurityMode = MessageSecurityMode.SignAndEncrypt,
            SecurityPolicyUri = SecurityPolicies.Basic256Sha256,
            SecurityLevel = 100,
            UserIdentityTokens = new UserTokenPolicyCollection
            {
                new UserTokenPolicy
                {
                    PolicyId = "user_none",
                    TokenType = UserTokenType.UserName,
                    SecurityPolicyUri = SecurityPolicies.None
                },
                new UserTokenPolicy
                {
                    PolicyId = "user_sha1",
                    TokenType = UserTokenType.UserName,
                    SecurityPolicyUri = SecurityPolicies.Basic128Rsa15
                },
                new UserTokenPolicy
                {
                    PolicyId = "user_sha256",
                    TokenType = UserTokenType.UserName,
                    SecurityPolicyUri = SecurityPolicies.Basic256Sha256
                }
            }
        };

        OpcUaUserIdentity userIdentity = OpcUaUserIdentity.CreateUserName("operator", "CorrectHorseBatteryStaple!");
        OpcUaClientOptions options = CreateSecureEndpointTestOptions(
            MessageSecurityMode.SignAndEncrypt,
            new[] { SecurityPolicies.Basic256Sha256 },
            userIdentity);

        ConfiguredEndpoint selected = OpcUaDiscovery.SelectEndpoint(new[] { serverEndpoint }, options);

        Assert.Equal(3, serverEndpoint.UserIdentityTokens.Count);
        UserTokenPolicy singleApprovedToken = Assert.Single(selected.Description.UserIdentityTokens);
        Assert.Equal("user_sha256", singleApprovedToken.PolicyId);

        IUserIdentity sdkIdentity = userIdentity.ToSdkIdentity(singleApprovedToken.PolicyId);
        UserNameIdentityToken token = Assert.IsType<UserNameIdentityToken>(sdkIdentity.GetIdentityToken());
        Assert.Equal("user_sha256", token.PolicyId);

        EndpointDescription unencryptedOnlyPlaintextTokenEndpoint = new()
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840",
            TransportProfileUri = Profiles.UaTcpTransport,
            SecurityMode = MessageSecurityMode.None,
            SecurityPolicyUri = SecurityPolicies.None,
            UserIdentityTokens = new UserTokenPolicyCollection
            {
                new UserTokenPolicy
                {
                    PolicyId = "user_none",
                    TokenType = UserTokenType.UserName,
                    SecurityPolicyUri = SecurityPolicies.None
                },
                new UserTokenPolicy
                {
                    PolicyId = "user_sha1",
                    TokenType = UserTokenType.UserName,
                    SecurityPolicyUri = SecurityPolicies.Basic256
                }
            }
        };

        OpcUaClientOptions strictNoneOptions = CreateSecureEndpointTestOptions(
            MessageSecurityMode.None,
            new[] { SecurityPolicies.None },
            userIdentity);

        Assert.Throws<InvalidOperationException>(() =>
            OpcUaDiscovery.SelectEndpoint(new[] { unencryptedOnlyPlaintextTokenEndpoint }, strictNoneOptions));
    }

    [Fact]
    public void S09_SelectEndpoint_PrioritizesClientPolicyRankAheadOfServerSecurityLevel()
    {
        EndpointDescription preferredPolicyLowerServerLevel = new()
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840",
            TransportProfileUri = Profiles.UaTcpTransport,
            SecurityMode = MessageSecurityMode.SignAndEncrypt,
            SecurityPolicyUri = SecurityPolicies.Aes256_Sha256_RsaPss,
            SecurityLevel = 10,
            UserIdentityTokens = new UserTokenPolicyCollection
            {
                new UserTokenPolicy { PolicyId = "anon", TokenType = UserTokenType.Anonymous }
            }
        };

        EndpointDescription weakerPolicyHigherServerLevel = new()
        {
            EndpointUrl = "opc.tcp://127.0.0.1:4840",
            TransportProfileUri = Profiles.UaTcpTransport,
            SecurityMode = MessageSecurityMode.SignAndEncrypt,
            SecurityPolicyUri = SecurityPolicies.Basic256Sha256,
            SecurityLevel = 250,
            UserIdentityTokens = new UserTokenPolicyCollection
            {
                new UserTokenPolicy { PolicyId = "anon", TokenType = UserTokenType.Anonymous }
            }
        };

        OpcUaClientOptions options = CreateSecureEndpointTestOptions(
            MessageSecurityMode.SignAndEncrypt,
            new[] { SecurityPolicies.Aes256_Sha256_RsaPss, SecurityPolicies.Basic256Sha256 },
            OpcUaUserIdentity.CreateAnonymous());

        ConfiguredEndpoint selected = OpcUaDiscovery.SelectEndpoint(
            new[] { weakerPolicyHigherServerLevel, preferredPolicyLowerServerLevel },
            options);

        Assert.Equal(SecurityPolicies.Aes256_Sha256_RsaPss, selected.Description.SecurityPolicyUri);
    }

    [Fact]
    public async Task S10_SelectEndpoint_PreservesApplicationConfigurationTransportQuotas()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"fieldlink-opcua-cfg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            OpcUaClientOptions options = new()
            {
                EndpointUrl = new Uri("opc.tcp://127.0.0.1:4840"),
                MessageSecurityMode = MessageSecurityMode.None,
                AllowedSecurityPolicies = new[] { SecurityPolicies.None },
                DefaultOperationTimeout = TimeSpan.FromSeconds(17),
                Certificates = new OpcUaCertificateOptions
                {
                    ApplicationCertificateStorePath = Path.Combine(tempRoot, "own"),
                    TrustedPeerCertificatesStorePath = Path.Combine(tempRoot, "trusted"),
                    TrustedIssuerCertificatesStorePath = Path.Combine(tempRoot, "issuer"),
                    RejectedCertificateStorePath = Path.Combine(tempRoot, "rejected")
                }
            };

            ApplicationConfiguration configuration = await OpcUaConfigurationFactory.CreateClientConfigurationAsync(
                options,
                CancellationToken.None);

            EndpointDescription endpoint = new()
            {
                EndpointUrl = "opc.tcp://127.0.0.1:4840",
                TransportProfileUri = Profiles.UaTcpTransport,
                SecurityMode = MessageSecurityMode.None,
                SecurityPolicyUri = SecurityPolicies.None,
                UserIdentityTokens = new UserTokenPolicyCollection
                {
                    new UserTokenPolicy { PolicyId = "anon", TokenType = UserTokenType.Anonymous }
                }
            };

            ConfiguredEndpoint selected = OpcUaDiscovery.SelectEndpoint(new[] { endpoint }, options, configuration);

            Assert.Equal(17_000, selected.Configuration.OperationTimeout);
            Assert.Equal(configuration.TransportQuotas.MaxMessageSize, selected.Configuration.MaxMessageSize);
            Assert.Equal(configuration.TransportQuotas.ChannelLifetime, selected.Configuration.ChannelLifetime);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void S11_RedactionAndOptionsValidation_EnforceShortPasswordBoundariesAndRangeGuards()
    {
        OpcUaUserIdentity singleCharIdentity = OpcUaUserIdentity.CreateUserName("user", "a");
        string unmangledMessage = "Failed calling ReadValueAsync at status Bad";
        Assert.False(singleCharIdentity.ContainsSensitiveData(unmangledMessage));
        Assert.Equal(unmangledMessage, singleCharIdentity.RedactPassword(unmangledMessage, "[REDACTED]"));

        OpcUaUserIdentity shortPasswordIdentity = OpcUaUserIdentity.CreateUserName("user", "pass");
        string sentenceWithSubstringAndToken = "bypassed with password pass!";
        Assert.True(shortPasswordIdentity.ContainsSensitiveData(sentenceWithSubstringAndToken));
        Assert.Equal("bypassed with password [REDACTED]!", shortPasswordIdentity.RedactPassword(sentenceWithSubstringAndToken, "[REDACTED]"));
        Assert.False(shortPasswordIdentity.ContainsSensitiveData("bypassed with password"));

        OpcUaUserIdentity longPasswordIdentity = OpcUaUserIdentity.CreateUserName("user", "WrongSecretPassword!42");
        Assert.Equal(
            "prefix[REDACTED]suffix",
            longPasswordIdentity.RedactPassword("prefixWrongSecretPassword!42suffix", "[REDACTED]"));

        OpcUaCertificateOptions invalidStoreType = new()
        {
            StoreType = (OpcUaCertificateStoreType)999,
            ApplicationCertificateStorePath = "own",
            TrustedPeerCertificatesStorePath = "trusted",
            TrustedIssuerCertificatesStorePath = "issuer",
            RejectedCertificateStorePath = "rejected"
        };
        OpcUaClientOptions storeTypeOptions = new()
        {
            EndpointUrl = new Uri("opc.tcp://127.0.0.1:4840"),
            MessageSecurityMode = MessageSecurityMode.None,
            AllowedSecurityPolicies = new[] { SecurityPolicies.None },
            Certificates = invalidStoreType
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => storeTypeOptions.ValidateAndClone());

        OpcUaClientOptions excessiveTimeoutOptions = CreateSecureEndpointTestOptions(
            MessageSecurityMode.None,
            new[] { SecurityPolicies.None },
            OpcUaUserIdentity.CreateAnonymous());
        OpcUaClientOptions oversizedTimeout = new()
        {
            EndpointUrl = excessiveTimeoutOptions.EndpointUrl,
            MessageSecurityMode = MessageSecurityMode.None,
            AllowedSecurityPolicies = new[] { SecurityPolicies.None },
            ConnectTimeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1000),
            Certificates = excessiveTimeoutOptions.Certificates
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => oversizedTimeout.ValidateAndClone());
    }

    private static OpcUaClientOptions CreateSecureEndpointTestOptions(
        MessageSecurityMode securityMode,
        IReadOnlyList<string> allowedPolicies,
        OpcUaUserIdentity identity)
    {
        return new OpcUaClientOptions
        {
            EndpointUrl = new Uri("opc.tcp://127.0.0.1:4840"),
            MessageSecurityMode = securityMode,
            AllowedSecurityPolicies = allowedPolicies,
            UserIdentity = identity,
            Certificates = new OpcUaCertificateOptions
            {
                ApplicationCertificateStorePath = "own",
                TrustedPeerCertificatesStorePath = "trusted",
                TrustedIssuerCertificatesStorePath = "issuer",
                RejectedCertificateStorePath = "rejected"
            }
        };
    }
}
