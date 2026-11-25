using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Security.Certificates;
using Opc.Ua.Server;

namespace FieldLink.OpcUa.Tests.Fixtures;

internal sealed class OpcUaTestServerOptions
{
    public bool IncludeSignAndEncryptEndpoint { get; init; } = true;

    public bool IncludeNoneEndpoint { get; init; }

    public bool AllowAnonymous { get; init; } = true;

    public string? Username { get; init; }

    public string? Password { get; init; }

    public bool UseExpiredServerCertificate { get; init; }

    public string? OverrideServerCertificateHost { get; init; }

    public string? OverrideServerCertificateAppUri { get; init; }

    public bool AutoAcceptClientCertificates { get; init; }
}

internal sealed class OpcUaTestServer : StandardServer, IAsyncDisposable
{
    private readonly string _rootDirectory;
    private readonly OpcUaTestServerOptions _options;
    private ApplicationConfiguration? _configuration;
    private OpcUaTestNodeManager? _nodeManager;
    private X509Certificate2? _serverCertificateWithPrivateKey;
    private X509Certificate2? _serverPublicCertificate;
    private bool _started;
    private bool _disposed;

    public OpcUaTestServer(OpcUaTestServerOptions? options = null)
    {
        _options = options ?? new OpcUaTestServerOptions();
        _rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "FieldLink.OpcUa.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);

        int port = ReserveLoopbackPort();
        EndpointUrl = new Uri($"opc.tcp://127.0.0.1:{port}");
    }

    public Uri EndpointUrl { get; }

    public string RootDirectory => _rootDirectory;

    public OpcUaTestNodeManager NodeManager
    {
        get
        {
            if (_nodeManager is null)
            {
                throw new InvalidOperationException("Server has not finished starting.");
            }

            return _nodeManager;
        }
    }

    public X509Certificate2 ServerPublicCertificate
    {
        get
        {
            if (_serverPublicCertificate is null)
            {
                throw new InvalidOperationException("Server has not finished starting.");
            }

            return LoadCertificate(_serverPublicCertificate.RawData);
        }
    }

    private static X509Certificate2 LoadCertificate(byte[] rawData)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(rawData);
#else
        return new X509Certificate2(rawData);
#endif
    }

    public static async Task<X509Certificate2> ProvisionCertificateAsync(
        string ownStorePath,
        string applicationUri,
        string applicationName,
        string[]? domainNames = null,
        bool expired = false,
        CancellationToken cancellationToken = default)
    {
        DateTime notBefore = expired
            ? DateTime.UtcNow.AddDays(-30)
            : DateTime.UtcNow.AddDays(-1);
        DateTime notAfter = expired
            ? DateTime.UtcNow.AddDays(-1)
            : DateTime.UtcNow.AddDays(30);

        string[] domains = domainNames ?? new[]
        {
            "127.0.0.1",
            "localhost",
            Utils.GetHostName(),
            Utils.GetFullQualifiedDomainName()
        };

        ICertificateBuilder builder = CertificateFactory
            .CreateCertificate(applicationUri, applicationName, $"CN={applicationName}", domains)
            .SetNotBefore(notBefore)
            .SetNotAfter(notAfter);
        X509Certificate2 cert = builder.CreateForRSA();

        Directory.CreateDirectory(ownStorePath);
        CertificateIdentifier storeId = new()
        {
            StoreType = CertificateStoreType.Directory,
            StorePath = ownStorePath
        };
        using ICertificateStore store = storeId.OpenStore(DefaultTelemetry.Create(static _ => { }));
        await store.AddAsync(cert, password: null, cancellationToken);
        return cert;
    }

    public static async Task TrustCertificateAsync(
        string trustedStorePath,
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(trustedStorePath);
        CertificateStoreIdentifier storeId = new()
        {
            StoreType = CertificateStoreType.Directory,
            StorePath = trustedStorePath
        };
        using ICertificateStore store = storeId.OpenStore(DefaultTelemetry.Create(_ => { }));
        using X509Certificate2 publicOnly = LoadCertificate(certificate.RawData);
        await store.AddAsync(publicOnly, password: null, cancellationToken);
    }

    public async Task StartServerAsync(CancellationToken cancellationToken = default)
    {
        string serverOwnPath = Path.Combine(_rootDirectory, "server", "own");
        string serverTrustedPath = Path.Combine(_rootDirectory, "server", "trusted");
        string serverIssuerPath = Path.Combine(_rootDirectory, "server", "issuer");
        string serverRejectedPath = Path.Combine(_rootDirectory, "server", "rejected");

        Directory.CreateDirectory(serverOwnPath);
        Directory.CreateDirectory(serverTrustedPath);
        Directory.CreateDirectory(serverIssuerPath);
        Directory.CreateDirectory(serverRejectedPath);

        string appUri = "urn:fieldlink:opcua:testserver";
        string certAppUri = _options.OverrideServerCertificateAppUri ?? appUri;
        string[]? certDomains = _options.OverrideServerCertificateHost is not null
            ? new[] { _options.OverrideServerCertificateHost }
            : null;

        _serverCertificateWithPrivateKey = await ProvisionCertificateAsync(
            serverOwnPath,
            certAppUri,
            "FieldLink OpcUa TestServer",
            certDomains,
            _options.UseExpiredServerCertificate,
            cancellationToken);
        _serverPublicCertificate = LoadCertificate(_serverCertificateWithPrivateKey.RawData);

        ServerSecurityPolicyCollection securityPolicies = new();
        if (_options.IncludeSignAndEncryptEndpoint)
        {
            securityPolicies.Add(new ServerSecurityPolicy
            {
                SecurityMode = MessageSecurityMode.SignAndEncrypt,
                SecurityPolicyUri = SecurityPolicies.Basic256Sha256
            });
            securityPolicies.Add(new ServerSecurityPolicy
            {
                SecurityMode = MessageSecurityMode.SignAndEncrypt,
                SecurityPolicyUri = SecurityPolicies.Aes256_Sha256_RsaPss
            });
        }

        if (_options.IncludeNoneEndpoint)
        {
            securityPolicies.Add(new ServerSecurityPolicy
            {
                SecurityMode = MessageSecurityMode.None,
                SecurityPolicyUri = SecurityPolicies.None
            });
        }

        UserTokenPolicyCollection userTokenPolicies = new();
        if (_options.AllowAnonymous)
        {
            userTokenPolicies.Add(new UserTokenPolicy(UserTokenType.Anonymous));
        }

        if (!string.IsNullOrEmpty(_options.Username))
        {
            userTokenPolicies.Add(new UserTokenPolicy(UserTokenType.UserName)
            {
                SecurityPolicyUri = SecurityPolicies.Basic256Sha256
            });
        }

        CertificateIdentifier serverAppCert = new()
        {
            StoreType = CertificateStoreType.Directory,
            StorePath = serverOwnPath,
            SubjectName = _serverCertificateWithPrivateKey.Subject,
            Thumbprint = _serverCertificateWithPrivateKey.Thumbprint,
            CertificateType = ObjectTypeIds.RsaSha256ApplicationCertificateType,
            Certificate = _serverCertificateWithPrivateKey
        };

        _configuration = new ApplicationConfiguration
        {
            ApplicationName = "FieldLink OpcUa TestServer",
            ApplicationUri = appUri,
            ProductUri = "urn:fieldlink:opcua:testserver",
            ApplicationType = ApplicationType.Server,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = serverAppCert,
                ApplicationCertificates = new CertificateIdentifierCollection { serverAppCert },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = serverTrustedPath
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = serverIssuerPath
                },
                RejectedCertificateStore = new CertificateStoreIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = serverRejectedPath
                },
                AutoAcceptUntrustedCertificates = _options.AutoAcceptClientCertificates,
                AddAppCertToTrustedStore = false,
                RejectSHA1SignedCertificates = false
            },
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 30_000,
                MaxStringLength = 1024 * 1024,
                MaxByteStringLength = 4 * 1024 * 1024,
                MaxArrayLength = 1024 * 1024
            },
            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = new StringCollection { EndpointUrl.ToString().TrimEnd('/') },
                SecurityPolicies = securityPolicies,
                UserTokenPolicies = userTokenPolicies,
                MinRequestThreadCount = 4,
                MaxRequestThreadCount = 16,
                MaxQueuedRequestCount = 200,
                MaxSessionCount = 20,
                MinSessionTimeout = 5_000,
                MaxSessionTimeout = 120_000,
                MaxPublishRequestCount = 20,
                MaxSubscriptionCount = 20,
                DiagnosticsEnabled = true
            }
        };

        await _configuration.ValidateAsync(ApplicationType.Server, cancellationToken);
        await _configuration.CertificateValidator.UpdateAsync(_configuration, cancellationToken);
        ApplicationInstance applicationInstance = new(_configuration, DefaultTelemetry.Create(static _ => { }))
        {
            DisableCertificateAutoCreation = true
        };
        await applicationInstance.StartAsync(this);
        _started = true;
    }

    public async Task TrustClientCertificateAsync(X509Certificate2 clientCertificate, CancellationToken cancellationToken = default)
    {
        string serverTrustedPath = Path.Combine(_rootDirectory, "server", "trusted");
        await TrustCertificateAsync(serverTrustedPath, clientCertificate, cancellationToken);
    }

    public int ActiveSubscriptionCount => ServerInternal.SubscriptionManager.GetSubscriptions().Count;

    public void ForceCloseAllSessions()
    {
        foreach (ISession session in ServerInternal.SessionManager.GetSessions())
        {
            ServerInternal.CloseSession(null, session.Id, deleteSubscriptions: true);
        }
    }

    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        _nodeManager = new OpcUaTestNodeManager(server, configuration);
        return new MasterNodeManager(server, configuration, null, _nodeManager);
    }

    protected override void OnServerStarted(IServerInternal server)
    {
        base.OnServerStarted(server);
        server.SessionManager.ImpersonateUser += OnImpersonateUser;
    }

    private void OnImpersonateUser(ISession session, ImpersonateEventArgs args)
    {
        if (args.NewIdentity is UserNameIdentityToken userNameToken)
        {
            string decodedPassword = userNameToken.DecryptedPassword is null
                ? string.Empty
                : Encoding.UTF8.GetString(userNameToken.DecryptedPassword);
            if (string.IsNullOrEmpty(_options.Username) ||
                !string.Equals(userNameToken.UserName, _options.Username, StringComparison.Ordinal) ||
                !string.Equals(decodedPassword, _options.Password, StringComparison.Ordinal))
            {
                throw new ServiceResultException(StatusCodes.BadUserAccessDenied, "Invalid username or password.");
            }

            args.Identity = new UserIdentity(userNameToken);
            return;
        }

        if (args.NewIdentity is AnonymousIdentityToken)
        {
            if (!_options.AllowAnonymous)
            {
                throw new ServiceResultException(StatusCodes.BadIdentityTokenRejected, "Anonymous login disabled.");
            }

            args.Identity = new UserIdentity(new AnonymousIdentityToken());
            return;
        }

        throw new ServiceResultException(StatusCodes.BadIdentityTokenInvalid, "Unsupported identity token.");
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed && _started)
        {
            _started = false;
            try
            {
                await StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (ServiceResultException)
            {
            }
        }

        if (!_disposed)
        {
            await DrainSdkRequestQueueAsync().ConfigureAwait(false);
        }

        Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            if (_started)
            {
                _started = false;
                try
                {
                    StopAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (ServiceResultException)
                {
                }
            }

            DrainSdkRequestQueueAsync().GetAwaiter().GetResult();
            _disposed = true;
            base.Dispose(disposing);
            _serverCertificateWithPrivateKey?.Dispose();
            _serverPublicCertificate?.Dispose();
            TryDeleteRootDirectory();
        }
        else
        {
            base.Dispose(disposing);
        }
    }

    private async Task DrainSdkRequestQueueAsync()
    {
        // OPCFoundation SDK ServerBase.RequestQueue.Dispose() disposes SemaphoreSlim before
        // WorkerLoopAsync tasks exit, which throws unobserved ArgumentNullException on net48.
        object? requestQueue = typeof(ServerBase)
            .GetField("m_requestQueue", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(this);
        if (requestQueue is null)
        {
            return;
        }

        Type queueType = requestQueue.GetType();
        queueType.GetField("m_stopped", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(requestQueue, true);

        if (queueType.GetField("m_cts", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(requestQueue) is CancellationTokenSource cancellationSource)
        {
            try
            {
                cancellationSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        if (queueType.GetField("m_workers", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(requestQueue) is List<Task> workers)
        {
            Task[] snapshot;
            lock (workers)
            {
                snapshot = workers.ToArray();
            }

            if (snapshot.Length > 0)
            {
                Task allWorkers = Task.WhenAll(snapshot);
                _ = allWorkers.ContinueWith(static completed => _ = completed.Exception, TaskContinuationOptions.OnlyOnFaulted);
                await Task.WhenAny(allWorkers, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
            }
        }
    }

    private static int ReserveLoopbackPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private void TryDeleteRootDirectory()
    {
        try
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
