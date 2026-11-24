namespace FieldLink.OpcUa.Internal;

internal static class OpcUaDiagnosticRedactor
{
    private const string RedactedPlaceholder = "****";

    public static string Redact(string? message, OpcUaUserIdentity? identity)
    {
        if (message is not { Length: > 0 })
        {
            return string.Empty;
        }

        if (identity is null || !identity.ContainsSensitiveData(message))
        {
            return message;
        }

        // 예외 메시지나 진단 문자열에 자격증명 비밀번호가 섞여 들어오는 경로를 방어한다.
        return identity.RedactPassword(message, RedactedPlaceholder);
    }
}
