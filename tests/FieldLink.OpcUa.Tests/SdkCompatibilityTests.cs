using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Opc.Ua;
using Opc.Ua.Client;
using Xunit;

namespace FieldLink.OpcUa.Tests;

public sealed class SdkCompatibilityTests
{
    [Fact]
    public void SdkClientTypes_OnTargetRuntime_AreResolvedAtExpectedVersion()
    {
        Assembly clientAssembly = typeof(Session).Assembly;
        Assembly coreAssembly = typeof(NodeId).Assembly;

        Assert.NotNull(clientAssembly);
        Assert.NotNull(coreAssembly);
        Assert.Equal(new Version(1, 5, 378, 0), clientAssembly.GetName().Version);
    }

    [Fact]
    public void ProductAssembly_DoesNotReferenceCoreLibrariesOrServerSdk()
    {
        string productAssemblyPath = Path.Combine(AppContext.BaseDirectory, "FieldLink.OpcUa.dll");
        Assert.True(File.Exists(productAssemblyPath), $"Product assembly not found at {productAssemblyPath}");

        Assembly productAssembly = Assembly.LoadFrom(productAssemblyPath);
        string[] referencedNames = productAssembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(referencedNames, static name => name.Equals("FieldLink.Transport", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, static name => name.Equals("FieldLink.Plc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, static name => name.Equals("FieldLink.Robot", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, static name => name.Equals("OPCFoundation.NetStandard.Opc.Ua.Server", StringComparison.OrdinalIgnoreCase));
    }
}
