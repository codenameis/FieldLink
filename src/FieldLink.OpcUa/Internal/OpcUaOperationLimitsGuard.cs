using System;

namespace FieldLink.OpcUa.Internal;

internal static class OpcUaOperationLimitsGuard
{
    public static void ValidateLimit(int requestedCount, uint? maxAllowed, string limitName)
    {
        if (maxAllowed is > 0 && requestedCount > maxAllowed.Value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedCount),
                $"요청 항목 수({requestedCount})가 서버 {limitName} 한도({maxAllowed.Value})를 초과합니다. 자동 분할은 지원하지 않습니다.");
        }
    }
}
