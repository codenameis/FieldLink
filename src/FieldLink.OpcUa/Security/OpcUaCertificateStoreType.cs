namespace FieldLink.OpcUa.Security;

/// <summary>X.509 인증서 저장소의 물리 저장 방식을 지정합니다.</summary>
public enum OpcUaCertificateStoreType
{
    /// <summary>파일 시스템 디렉터리 기반 저장소(<c>Directory</c>)를 사용합니다.</summary>
    Directory = 0,

    /// <summary>Windows/OS X.509 인증서 저장소(<c>X509Store</c>)를 사용합니다.</summary>
    X509Store = 1
}
