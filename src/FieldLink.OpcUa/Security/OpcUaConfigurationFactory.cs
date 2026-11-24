using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;

namespace FieldLink.OpcUa.Security;

internal static class OpcUaConfigurationFactory
{
    private const string DefaultProductUri = "urn:fieldlink:opcua:client";
    private const int MinimumOperationTimeoutMilliseconds = 1_000;
    private const int DefaultSecurityTokenLifetimeMilliseconds = 3_600_000;
    private const int DefaultChannelLifetimeMilliseconds = 300_000;
    private const int DefaultMaxStringLength = 1_048_576;
    private const int DefaultMaxByteStringLength = 4_194_304;
    private const int DefaultMaxArrayLength = 1_048_576;
    private const int DefaultMaxMessageSize = 4_194_304;
    private const int DefaultMaxBufferSize = 65_535;

    public static async Task<ApplicationConfiguration> CreateClientConfigurationAsync(
        OpcUaClientOptions options,
        CancellationToken cancellationToken)
    {
        CertificateIdentifier applicationCertificate = await ResolveClientCertificateAsync(
            options,
            cancellationToken).ConfigureAwait(false);

        ApplicationConfiguration configuration = new()
        {
            ApplicationName = options.ApplicationName,
            ApplicationUri = options.ApplicationUri,
            ProductUri = DefaultProductUri,
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = applicationCertificate,
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = options.Certificates.SdkStoreTypeName,
                    StorePath = options.Certificates.TrustedPeerCertificatesStorePath
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = options.Certificates.SdkStoreTypeName,
                    StorePath = options.Certificates.TrustedIssuerCertificatesStorePath
                },
                RejectedCertificateStore = new CertificateStoreIdentifier
                {
                    StoreType = options.Certificates.SdkStoreTypeName,
                    StorePath = options.Certificates.RejectedCertificateStorePath
                },
                // 클린룸 보안 규칙: 미신뢰 인증서는 절대 자동 승인하지 않는다.
                AutoAcceptUntrustedCertificates = false,
                AddAppCertToTrustedStore = false,
                RejectSHA1SignedCertificates = options.Certificates.RejectSha1SignedCertificates
            },
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = Math.Max(MinimumOperationTimeoutMilliseconds, (int)options.DefaultOperationTimeout.TotalMilliseconds),
                SecurityTokenLifetime = DefaultSecurityTokenLifetimeMilliseconds,
                ChannelLifetime = DefaultChannelLifetimeMilliseconds,
                MaxStringLength = DefaultMaxStringLength,
                MaxByteStringLength = DefaultMaxByteStringLength,
                MaxArrayLength = DefaultMaxArrayLength,
                MaxMessageSize = DefaultMaxMessageSize,
                MaxBufferSize = DefaultMaxBufferSize
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = (int)Math.Min(int.MaxValue, options.SessionTimeoutMilliseconds)
            }
        };

        await configuration.ValidateAsync(ApplicationType.Client, cancellationToken).ConfigureAwait(false);
        await configuration.CertificateValidator.UpdateAsync(configuration, cancellationToken).ConfigureAwait(false);
        return configuration;
    }

    private static async Task<CertificateIdentifier> ResolveClientCertificateAsync(
        OpcUaClientOptions options,
        CancellationToken cancellationToken)
    {
        OpcUaCertificateOptions certificateOptions = options.Certificates;
        CertificateIdentifier certificateIdentifier = new()
        {
            StoreType = certificateOptions.SdkStoreTypeName,
            StorePath = certificateOptions.ApplicationCertificateStorePath,
            SubjectName = certificateOptions.ApplicationCertificateSubjectName,
            Thumbprint = certificateOptions.ApplicationCertificateThumbprint,
            CertificateType = ObjectTypeIds.RsaSha256ApplicationCertificateType
        };

        if (options.MessageSecurityMode == MessageSecurityMode.None)
        {
            return certificateIdentifier;
        }

        if (certificateOptions.StoreType == OpcUaCertificateStoreType.Directory &&
            !Directory.Exists(certificateOptions.ApplicationCertificateStorePath))
        {
            throw new InvalidOperationException(
                "클라이언트 Own 인증서 저장소가 존재하지 않습니다. 연결 전에 인증서 및 개인키를 사전 프로비저닝해야 합니다.");
        }

        ITelemetryContext telemetry = DefaultTelemetry.Create(static _ => { });
        X509Certificate2? foundCertificate = await FindCertificateWithPrivateKeyAsync(
            certificateIdentifier,
            options.ApplicationUri,
            telemetry,
            cancellationToken).ConfigureAwait(false);

        if (foundCertificate is null)
        {
            throw new InvalidOperationException(
                "보안 연결에 필요한 클라이언트 애플리케이션 인증서 또는 개인키가 Own 저장소에 없습니다. " +
                "연결 중 자동 발급은 허용되지 않으므로 사전에 인증서를 프로비저닝하세요.");
        }

        certificateIdentifier.Certificate = foundCertificate;
        certificateIdentifier.SubjectName = foundCertificate.Subject;
        certificateIdentifier.Thumbprint = foundCertificate.Thumbprint;
        return certificateIdentifier;
    }

    private static async Task<X509Certificate2?> FindCertificateWithPrivateKeyAsync(
        CertificateIdentifier certificateIdentifier,
        string applicationUri,
        ITelemetryContext telemetry,
        CancellationToken cancellationToken)
    {
        X509Certificate2? direct = await certificateIdentifier
            .FindAsync(true, applicationUri, telemetry, cancellationToken)
            .ConfigureAwait(false);
        if (direct is { HasPrivateKey: true })
        {
            return direct;
        }

        direct?.Dispose();

        using ICertificateStore store = certificateIdentifier.OpenStore(telemetry);
        X509Certificate2Collection candidates = await store.EnumerateAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                X509Certificate2 candidate = candidates[i];
                if (!MatchesApplicationUriOrFilter(candidate, certificateIdentifier, applicationUri))
                {
                    continue;
                }

                X509Certificate2? loaded = await store.LoadPrivateKeyAsync(
                    candidate.Thumbprint,
                    candidate.Subject,
                    applicationUri,
                    ObjectTypeIds.RsaSha256ApplicationCertificateType,
                    null,
                    cancellationToken).ConfigureAwait(false);
                if (loaded is { HasPrivateKey: true })
                {
                    return loaded;
                }

                loaded?.Dispose();
            }

            return null;
        }
        finally
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                candidates[i].Dispose();
            }
        }
    }

    private static bool MatchesApplicationUriOrFilter(
        X509Certificate2 candidate,
        CertificateIdentifier filter,
        string applicationUri)
    {
        if (!string.IsNullOrWhiteSpace(filter.Thumbprint) &&
            !string.Equals(candidate.Thumbprint, filter.Thumbprint, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.SubjectName) &&
            !X509Utils.CompareDistinguishedName(candidate.Subject, filter.SubjectName))
        {
            return false;
        }

        System.Collections.Generic.IReadOnlyList<string> sanUris = X509Utils.GetApplicationUrisFromCertificate(candidate);
        for (int i = 0; i < sanUris.Count; i++)
        {
            if (string.Equals(sanUris[i], applicationUri, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
