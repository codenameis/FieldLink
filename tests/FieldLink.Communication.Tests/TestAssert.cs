using FieldLink.Communication.Diagnostics;
using FieldLink.Communication;

namespace FieldLink.Communication.Tests;

internal static class TestAssert
{
    internal static void True(bool condition, string message = "Assertion failed")
    {
        if (!condition)
            throw new Exception(message);
    }

    internal static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}; actual {actual}");
    }

    internal static void Bytes(byte[] expected, byte[] actual)
    {
        if (!expected.SequenceEqual(actual))
            throw new Exception($"Expected {TestBytes.ToHexString(expected)}; actual {TestBytes.ToHexString(actual)}");
    }

    internal static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T error) { return error; }
        throw new Exception($"Expected {typeof(T).Name}");
    }

    internal static async Task<CommunicationException> FailureAsync(Func<Task> action, CommunicationFailure failure)
    {
        var error = await ThrowsAsync<CommunicationException>(action);
        Equal(failure, error.Failure);
        True(error.InnerException != null, "Cause must be preserved");
        return error;
    }
}
