using FieldLink.Communication.Tests;

// .NET Standard는 API 계약이므로 실행 진입점만 호스트에 둔다. 테스트와 판정은 Tests에서 수행한다.
internal static class Program
{
    private static int Main(string[] args) => TestSuite.RunAsync(args).GetAwaiter().GetResult();
}
