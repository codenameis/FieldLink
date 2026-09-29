using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace FieldLink.Communication.Tests;

internal static class ApiContractTests
{
    internal static Task PublicApiMatchesReviewedSnapshotAsync()
    {
        string[] expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Contracts", "public-api.txt"));
        string[] actual = ApiSurface.Read();
        TestAssert.True(expected.SequenceEqual(actual),
            "API changes require review. Removed: " + string.Join("\n", expected.Except(actual).Take(15)) +
            "\nAdded: " + string.Join("\n", actual.Except(expected).Take(15)));
        return Task.CompletedTask;
    }

    internal static Task RuntimeDependenciesAndFrameworksRemainIndependentAsync()
    {
        var product = typeof(ICommunicationClient).Assembly;
        string[] allowed = ["netstandard", "System.IO.Ports", "Newtonsoft.Json"];
        foreach (var reference in product.GetReferencedAssemblies())
            TestAssert.True(allowed.Contains(reference.Name), "Unexpected product dependency: " + reference.FullName);
        TestAssert.Equal(".NETStandard,Version=v2.0", product.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        TestAssert.Equal(".NETStandard,Version=v2.0", typeof(ApiContractTests).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        TestAssert.Equal(".NETFramework,Version=v4.8", Assembly.GetEntryAssembly()!.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        TestAssert.True(product.GetExportedTypes().All(t => t.Namespace != null && !t.Namespace.Contains(".Profinet")));
        return Task.CompletedTask;
    }

    internal static Task GoldenFixturesMatchReviewedHashesAsync()
    {
        var manifest = JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "fixture-integrity.json")));
        using var sha = SHA256.Create();
        foreach (var entry in manifest)
        {
            string name = Path.GetFileName(entry["file"]!.Value<string>()!);
            string content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)).Replace("\r\n", "\n");
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
            string actual = string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            TestAssert.Equal(entry["normalizedTextSha256"]!.Value<string>(), actual);
        }
        return Task.CompletedTask;
    }
}
