using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Opc.Ua;
using Opc.Ua.Client;
using Xunit;

namespace FieldLink.OpcUa.Tests;

public sealed class ApiContractTests
{
    private static readonly Type[] ForbiddenExposedSdkTypes =
    {
        typeof(Session),
        typeof(ISession),
        typeof(Subscription),
        typeof(MonitoredItem),
        typeof(ApplicationConfiguration),
        typeof(DiscoveryClient)
    };

    [Fact]
    public void U010_PublicApiSurface_MatchesSnapshotAndEncapsulatesLowLevelSdkTypes()
    {
        Assembly productAssembly = typeof(IOpcUaClient).Assembly;
        Type[] exportedTypes = productAssembly
            .GetExportedTypes()
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        string[] actualLines = BuildCanonicalApiLines(exportedTypes);
        string snapshotPath = Path.Combine(AppContext.BaseDirectory, "public-api.txt");
        Assert.True(File.Exists(snapshotPath), $"Public API snapshot missing at {snapshotPath}");

        string[] expectedLines = File.ReadAllLines(snapshotPath);
        Assert.Equal(expectedLines, actualLines);

        foreach (Type exportedType in exportedTypes)
        {
            AssertNoForbiddenSdkTypes(exportedType);
        }
    }

    private static string[] BuildCanonicalApiLines(Type[] exportedTypes)
    {
        List<string> lines = new();
        foreach (Type type in exportedTypes)
        {
            lines.Add($"T: {type.FullName}");

            foreach (ConstructorInfo ctor in type.GetConstructors().OrderBy(static c => c.ToString(), StringComparer.Ordinal))
            {
                lines.Add($"  C: {ctor}");
            }

            foreach (PropertyInfo property in type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .OrderBy(static p => p.Name, StringComparer.Ordinal))
            {
                lines.Add($"  P: {property.PropertyType.Name} {property.Name}");
            }

            foreach (MethodInfo method in type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static m => !m.IsSpecialName)
                .OrderBy(static m => m.ToString(), StringComparer.Ordinal))
            {
                lines.Add($"  M: {method}");
            }

            if (type.IsEnum)
            {
                foreach (string enumName in Enum.GetNames(type))
                {
                    lines.Add($"  E: {enumName}");
                }
            }
        }

        return lines.ToArray();
    }

    private static void AssertNoForbiddenSdkTypes(Type exportedType)
    {
        foreach (PropertyInfo property in exportedType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            AssertTypeNotForbidden(property.PropertyType, $"{exportedType.Name}.{property.Name}");
        }

        foreach (MethodInfo method in exportedType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            AssertTypeNotForbidden(method.ReturnType, $"{exportedType.Name}.{method.Name} return");
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertTypeNotForbidden(parameter.ParameterType, $"{exportedType.Name}.{method.Name}({parameter.Name})");
            }
        }
    }

    private static void AssertTypeNotForbidden(Type type, string memberLocation)
    {
        foreach (Type forbidden in ForbiddenExposedSdkTypes)
        {
            Assert.False(forbidden.IsAssignableFrom(type), $"Forbidden SDK type {forbidden.Name} exposed at {memberLocation}");
        }

        if (type.IsGenericType)
        {
            foreach (Type genericArgument in type.GetGenericArguments())
            {
                AssertTypeNotForbidden(genericArgument, memberLocation);
            }
        }
    }
}
