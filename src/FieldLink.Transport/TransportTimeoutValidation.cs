using System;

namespace FieldLink.Transport;

internal static class TransportTimeoutValidation
{
    private const int MinimumPositiveMilliseconds = 1;

    public static readonly TimeSpan MaximumTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    public static void ValidatePositive(TimeSpan value, string parameterName, string message)
    {
        if (value <= TimeSpan.Zero || value > MaximumTimeout)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, message);
        }
    }

    public static void ValidateNonNegative(TimeSpan value, string parameterName, string message)
    {
        if (value < TimeSpan.Zero || value > MaximumTimeout)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, message);
        }
    }

    public static int ToPositiveMillisecondsCeiling(TimeSpan value)
    {
        double ceilingMilliseconds = Math.Ceiling(value.TotalMilliseconds);
        if (ceilingMilliseconds >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return Math.Max(MinimumPositiveMilliseconds, (int)ceilingMilliseconds);
    }
}
