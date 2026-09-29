using System.Diagnostics;
using System.Reflection;

namespace FieldLink.Communication.Tests;

/// <summary>Standard 2.0 테스트 모음을 기존 필터·종료 코드 규칙으로 실행합니다.</summary>
public static class TestSuite
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args == null)
            throw new ArgumentNullException(nameof(args));
        if (args.Length == 2 && args[0] == "--write-api")
        {
            string[] api = ApiSurface.Read();
            File.WriteAllLines(args[1], api);
            Console.WriteLine($"API: {api.Length} declarations written");
            return 0;
        }
        // NuGet 테스트 프레임워크 없이 실행되는 회귀 테스트. 실패 시 종료 코드 1.
        var watch = Stopwatch.StartNew();
        int passed = 0;
        int failed = 0;
        Type[] protocolSuites = [typeof(SimplePlcTests), typeof(McClientTests), typeof(RobotClientTests), typeof(TcpProtocolSessionTests), typeof(FinsTcpSessionTests), typeof(ProtocolMigrationTests), typeof(RobotProtocolTests), typeof(MelsecQnA3ETests), typeof(MelsecMc3EBinaryClientTests), typeof(ProtocolResponseTests), typeof(ProtocolWriteTests), typeof(ProtocolFrameTests), typeof(ProtocolLoopbackTests)];
        bool protocolsOnly = args.Length > 0 && args[0] == "--protocols";
        string? filter = args.Skip(protocolsOnly ? 1 : 0).FirstOrDefault();
        Type[] suites = protocolsOnly ? protocolSuites :
            [typeof(ApiContractTests), typeof(AddressContractTests), typeof(ValueConverterTests), typeof(ChecksumTests), typeof(SerialTests), typeof(FrameTests), typeof(TcpTests), typeof(UdpTests), typeof(WebSocketTransportTests), typeof(PushServiceTests), typeof(FileTransferTests), typeof(ClientTests), typeof(ConnectionTests), typeof(DeviceClientTests), typeof(DeviceLifecycleTests), typeof(RequestEntryTests), ..protocolSuites];
        foreach (Type suite in suites)
        {
            foreach (MethodInfo method in suite.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method => method.IsAssembly && method.ReturnType == typeof(Task) && method.GetParameters().Length == 0))
            {
                string name = $"{suite.Name}.{method.Name}";
                if (!string.IsNullOrEmpty(filter) && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                try
                {
                    var test = (Func<Task>)method.CreateDelegate(typeof(Func<Task>));
                    await test().WaitAsync(TimeSpan.FromSeconds(12));
                    Console.WriteLine($"PASS {name}");
                    passed++;
                }
                catch (Exception error)
                {
                    Console.WriteLine($"FAIL {name}\n{error}");
                    failed++;
                }
            }
        }
        Console.WriteLine($"Result: {passed} passed, {failed} failed ({watch.Elapsed.TotalSeconds:F2}s)");
        return failed == 0 && passed > 0 ? 0 : 1;
    }
}
