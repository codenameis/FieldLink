namespace FieldLink.OpcUa;

/// <summary>OPC UA 세션 활성화에 사용할 사용자 인증 방식을 지정합니다.</summary>
public enum OpcUaUserIdentityType
{
    /// <summary>익명 사용자 토큰(<c>AnonymousIdentityToken</c>)을 사용합니다.</summary>
    Anonymous = 0,

    /// <summary>사용자 이름과 비밀번호(<c>UserNameIdentityToken</c>)를 사용합니다.</summary>
    UserName = 1
}
