using System;

namespace FieldLink.Transport;

internal static class TransportHeaderValidation
{
    private static readonly char[] ForbiddenControlCharacters = { '\r', '\n', '\0' };

    public static void ThrowIfContainsControlCharacters(string? value, string parameterName, string errorMessage)
    {
        if (value is not null && value.IndexOfAny(ForbiddenControlCharacters) >= 0)
        {
            throw new ArgumentException(errorMessage, parameterName);
        }
    }
}
