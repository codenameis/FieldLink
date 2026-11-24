using System;
using Opc.Ua;

namespace FieldLink.OpcUa.Security;

/// <summary>OPC UA 4개 필수 인증서 저장소(`Own`/`Trusted`/`Issuer`/`Rejected`) 및 엄격 검증 설정입니다.</summary>
/// <remarks>신뢰 목록에 없는 서버 인증서를 자동 승인하는 옵션은 보안 정책상 제공하지 않습니다.</remarks>
public sealed class OpcUaCertificateOptions
{
    /// <summary>인증서 저장소 유형을 지정하거나 가져옵니다. 기본값은 <see cref="OpcUaCertificateStoreType.Directory"/>입니다.</summary>
    public OpcUaCertificateStoreType StoreType { get; init; } = OpcUaCertificateStoreType.Directory;

    /// <summary>클라이언트 자신의 X.509 인증서와 개인키가 저장된 <c>Own</c> 저장소 경로를 지정하거나 가져옵니다.</summary>
    public string ApplicationCertificateStorePath { get; init; } = string.Empty;

    /// <summary>신뢰하는 서버 인증서 및 신뢰 CA가 저장된 <c>Trusted</c> 저장소 경로를 지정하거나 가져옵니다.</summary>
    public string TrustedPeerCertificatesStorePath { get; init; } = string.Empty;

    /// <summary>체인 검증에만 사용하는 중간 발급자 CA 저장소(<c>Issuer</c>) 경로를 지정하거나 가져옵니다.</summary>
    public string TrustedIssuerCertificatesStorePath { get; init; } = string.Empty;

    /// <summary>검증이 거절된 서버 인증서를 기록할 <c>Rejected</c> 저장소 경로를 지정하거나 가져옵니다.</summary>
    public string RejectedCertificateStorePath { get; init; } = string.Empty;

    /// <summary>클라이언트 <c>Own</c> 인증서를 찾을 때 사용할 주체 이름(Subject Name)입니다. 비어 있으면 URI 기준으로 탐색합니다.</summary>
    public string? ApplicationCertificateSubjectName { get; init; }

    /// <summary>클라이언트 <c>Own</c> 인증서를 찾을 때 사용할 지문(Thumbprint)입니다.</summary>
    public string? ApplicationCertificateThumbprint { get; init; }

    /// <summary>SHA-1 서명 인증서를 거부할지 여부를 지정하거나 가져옵니다. 기본값은 <see langword="true"/>입니다.</summary>
    public bool RejectSha1SignedCertificates { get; init; } = true;

    internal string SdkStoreTypeName =>
        StoreType == OpcUaCertificateStoreType.X509Store
            ? CertificateStoreType.X509Store
            : CertificateStoreType.Directory;

    internal void Validate(MessageSecurityMode securityMode)
    {
        if (securityMode != MessageSecurityMode.None &&
            string.IsNullOrWhiteSpace(ApplicationCertificateStorePath))
        {
            throw new ArgumentException(
                "보안 연결(Sign 또는 SignAndEncrypt)에는 클라이언트 Own 인증서 저장소 경로가 필수입니다.",
                nameof(ApplicationCertificateStorePath));
        }

        if (string.IsNullOrWhiteSpace(TrustedPeerCertificatesStorePath))
        {
            throw new ArgumentException(
                "신뢰 서버 인증서 저장소(Trusted) 경로는 필수입니다.",
                nameof(TrustedPeerCertificatesStorePath));
        }

        if (string.IsNullOrWhiteSpace(TrustedIssuerCertificatesStorePath))
        {
            throw new ArgumentException(
                "발급자 인증서 저장소(Issuer) 경로는 필수입니다.",
                nameof(TrustedIssuerCertificatesStorePath));
        }

        if (string.IsNullOrWhiteSpace(RejectedCertificateStorePath))
        {
            throw new ArgumentException(
                "거절 인증서 저장소(Rejected) 경로는 필수입니다.",
                nameof(RejectedCertificateStorePath));
        }
    }

    internal OpcUaCertificateOptions Clone()
    {
        return new OpcUaCertificateOptions
        {
            StoreType = StoreType,
            ApplicationCertificateStorePath = ApplicationCertificateStorePath,
            TrustedPeerCertificatesStorePath = TrustedPeerCertificatesStorePath,
            TrustedIssuerCertificatesStorePath = TrustedIssuerCertificatesStorePath,
            RejectedCertificateStorePath = RejectedCertificateStorePath,
            ApplicationCertificateSubjectName = ApplicationCertificateSubjectName,
            ApplicationCertificateThumbprint = ApplicationCertificateThumbprint,
            RejectSha1SignedCertificates = RejectSha1SignedCertificates
        };
    }
}
