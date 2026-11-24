using System;
using System.Text;
using Opc.Ua;

namespace FieldLink.OpcUa;

/// <summary>OPC UA <c>ActivateSession</c>에 사용할 불변 사용자 자격증명입니다. 비밀번호는 문자열 출력과 진단에 노출되지 않습니다.</summary>
public sealed class OpcUaUserIdentity
{
    private const string AnonymousDisplayName = "Anonymous";
    private const string RedactedPasswordMask = "****";

    private readonly string? _password;

    private OpcUaUserIdentity(OpcUaUserIdentityType identityType, string? username, string? password)
    {
        IdentityType = identityType;
        Username = username;
        _password = password;
    }

    /// <summary>사용자 인증 방식을 가져옵니다.</summary>
    public OpcUaUserIdentityType IdentityType { get; }

    /// <summary>사용자 이름 인증 시 사용할 계정 이름을 가져옵니다. 익명 인증일 때는 <see langword="null"/>입니다.</summary>
    public string? Username { get; }

    /// <summary>익명 사용자 자격증명을 생성합니다.</summary>
    public static OpcUaUserIdentity CreateAnonymous()
    {
        return new OpcUaUserIdentity(OpcUaUserIdentityType.Anonymous, null, null);
    }

    /// <summary>사용자 이름과 비밀번호 기반 자격증명을 생성합니다.</summary>
    /// <param name="username">서버에 전달할 비어 있지 않은 사용자 이름입니다.</param>
    /// <param name="password">서버에 전달할 비밀번호입니다.</param>
    public static OpcUaUserIdentity CreateUserName(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("사용자 이름은 비어 있을 수 없습니다.", nameof(username));
        }

        if (password is null)
        {
            throw new ArgumentNullException(nameof(password));
        }

        return new OpcUaUserIdentity(OpcUaUserIdentityType.UserName, username, password);
    }

    /// <summary>민감 정보를 마스킹한 자격증명 요약을 반환합니다.</summary>
    public override string ToString()
    {
        if (IdentityType == OpcUaUserIdentityType.Anonymous)
        {
            return AnonymousDisplayName;
        }

        return $"UserName({Username}:{RedactedPasswordMask})";
    }

    internal IUserIdentity ToSdkIdentity()
    {
        if (IdentityType == OpcUaUserIdentityType.Anonymous)
        {
            return new UserIdentity(new AnonymousIdentityToken());
        }

        byte[] passwordBytes = Encoding.UTF8.GetBytes(_password ?? string.Empty);
        // CreateUserName 생성 시 비어 있지 않은 Username이 이미 검증되었다.
        return new UserIdentity(Username!, passwordBytes);
    }

    internal OpcUaUserIdentity Clone()
    {
        return new OpcUaUserIdentity(IdentityType, Username, _password);
    }

    internal bool ContainsSensitiveData(string? text)
    {
        if (text is not { Length: > 0 } || _password is not { Length: > 0 })
        {
            return false;
        }

        return text.IndexOf(_password, StringComparison.Ordinal) >= 0;
    }

    internal string RedactPassword(string message, string replacement)
    {
        if (message.Length == 0 || _password is not { Length: > 0 })
        {
            return message;
        }

        return message.Replace(_password, replacement);
    }
}
