using System.Reflection;

namespace FieldLink.Communication.Tests;

internal static class TestAllocationCounter
{
    // API는 Standard 2.0 계약 밖이지만 Framework 4.8 호스트에 있다. 대체 측정이나 테스트 생략은 하지 않는다.
    // https://learn.microsoft.com/dotnet/api/system.gc.getallocatedbytesforcurrentthread
    internal static readonly Func<long> GetAllocatedBytesForCurrentThread = CreateCounter();

    private static Func<long> CreateCounter()
    {
        MethodInfo? method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
        if (method == null)
            throw new PlatformNotSupportedException("이 테스트 호스트는 스레드별 메모리 할당량 측정 API가 필요합니다.");
        return (Func<long>)method.CreateDelegate(typeof(Func<long>));
    }
}
