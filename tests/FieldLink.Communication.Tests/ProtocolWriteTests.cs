using Newtonsoft.Json.Linq;

namespace FieldLink.Communication.Tests;

// 수정하지 않은 참고 소스에서 생성한 쓰기 명령과 전체 바이트·결과 필드를 비교한다.
internal static class ProtocolWriteTests
{
    internal static Task AllenBradleyAsync() => Check("AllenBradley", 6);
    internal static Task BeckhoffAsync() => Check("Beckhoff", 4);
    internal static Task FatekAsync() => Check("FATEK", 2);
    internal static Task FujiAsync() => Check("Fuji", 4);
    internal static Task GeAsync() => Check("GE", 2);
    internal static Task KeyenceAsync() => Check("Keyence", 3);
    internal static Task LsisAsync() => Check("LSIS", 2);
    internal static Task MelsecAsync() => Check("Melsec", 9);
    internal static Task OmronAsync() => Check("Omron", 1);
    internal static Task PanasonicAsync() => Check("Panasonic", 3);
    internal static Task SiemensAsync() => Check("Siemens", 4);
    internal static Task ToyotaAsync() => Check("Toyota", 2);
    internal static Task TurckAsync() => Check("Turck", 1);
    internal static Task VigorAsync() => Check("Vigor", 2);
    internal static Task XinjeAsync() => Check("XINJE", 2);
    internal static Task YamatakeAsync() => Check("Yamatake", 1);
    internal static Task YaskawaAsync() => Check("YASKAWA", 2);
    internal static Task YokogawaAsync() => Check("Yokogawa", 4);

    internal static Task FixtureContainsSelectedWriteCasesAsync()
    {
        var document = Load();
        TestAssert.Equal(54, document.Count);
        TestAssert.Equal(18, document
            .Select(vector => vector["Destination"]!.Value<string>()!.Split('.')[2])
            .Distinct().Count());
        return Task.CompletedTask;
    }

    private static Task Check(string company, int expectedCount)
    {
        var document = Load();
        var vectors = document
            .Where(vector => vector["Destination"]!.Value<string>()!
                .StartsWith($"FieldLink.PlcDrivers.{company}.", StringComparison.Ordinal))
            .ToArray();
        TestAssert.Equal(expectedCount, vectors.Length);
        foreach (var vector in vectors) ProtocolMigrationTests.AssertGoldenVector(vector);
        return Task.CompletedTask;
    }

    private static JArray Load() => JArray.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "profinet-golden.writes.json")));
}
