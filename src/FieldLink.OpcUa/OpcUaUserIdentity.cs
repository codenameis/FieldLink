using System;
using System.Text;
using Opc.Ua;

namespace FieldLink.OpcUa;

/// <summary>OPC UA <c>ActivateSession</c>에 사용할 불변 사용자 자격증명입니다. 비밀번호는 문자열 출력과 진단에 노출되지 않습니다.</summary>
public sealed class OpcUaUserIdentity
{
    private const string AnonymousDisplayName = "Anonymous";
    private const string RedactedPasswordMask = "****";
    private const int MinimumRedactablePasswordLength = 4;
    private const int UnconditionalRedactLength = 8;

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

    internal IUserIdentity ToSdkIdentity(string? policyId = null)
    {
        if (IdentityType == OpcUaUserIdentityType.Anonymous)
        {
            AnonymousIdentityToken anonymousToken = new();
            if (!string.IsNullOrEmpty(policyId))
            {
                anonymousToken.PolicyId = policyId;
            }

            return new UserIdentity(anonymousToken);
        }

        byte[] passwordBytes = Encoding.UTF8.GetBytes(_password ?? string.Empty);
        UserNameIdentityToken userNameToken = new()
        {
            // CreateUserName 생성 시 비어 있지 않은 Username이 이미 검증되었다.
            UserName = Username ?? string.Empty,
            DecryptedPassword = passwordBytes
        };
        if (!string.IsNullOrEmpty(policyId))
        {
            userNameToken.PolicyId = policyId;
        }

        return new UserIdentity(userNameToken);
    }

    internal OpcUaUserIdentity Clone()
    {
        return new OpcUaUserIdentity(IdentityType, Username, _password);
    }

    internal bool ContainsSensitiveData(string? text)
    {
        if (text is not { Length: > 0 } ||
            _password is not { Length: >= MinimumRedactablePasswordLength } password)
        {
            return false;
        }

        if (password.Length >= UnconditionalRedactLength)
        {
            return text.IndexOf(password, StringComparison.Ordinal) >= 0;
        }

        return FindNextDelimitedMatch(text, password, 0) >= 0;
    }

    internal string RedactPassword(string message, string replacement)
    {
        if (message.Length == 0 ||
            _password is not { Length: >= MinimumRedactablePasswordLength } password)
        {
            return message;
        }

        if (password.Length >= UnconditionalRedactLength)
        {
            return message.Replace(password, replacement);
        }

        int matchIndex = FindNextDelimitedMatch(message, password, 0);
        if (matchIndex < 0)
        {
            return message;
        }

        StringBuilder builder = new(message.Length);
        int cursor = 0;
        while (matchIndex >= 0)
        {
            builder.Append(message, cursor, matchIndex - cursor);
            builder.Append(replacement);
            cursor = matchIndex + password.Length;
            matchIndex = FindNextDelimitedMatch(message, password, cursor);
        }

        builder.Append(message, cursor, message.Length - cursor);
        return builder.ToString();
    }

    private static int FindNextDelimitedMatch(string text, string password, int startIndex)
    {
        int searchIndex = startIndex;
        while (searchIndex <= text.Length - password.Length)
        {
            int matchIndex = text.IndexOf(password, searchIndex, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                return -1;
            }

            if (IsDelimitedToken(text, matchIndex, password.Length))
            {
                return matchIndex;
            }

            searchIndex = matchIndex + 1;
        }

        return -1;
    }

    private static bool IsDelimitedToken(string text, int startIndex, int length)
    {
        bool leftBoundaryOk = startIndex == 0 || !char.IsLetterOrDigit(text[startIndex - 1]);
        int endIndex = startIndex + length;
        bool rightBoundaryOk = endIndex == text.Length || !char.IsLetterOrDigit(text[endIndex]);
        return leftBoundaryOk && rightBoundaryOk;
    }
}
