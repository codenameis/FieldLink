using System.Text;
using FieldLink.Robot.FANUC.Protocols;

namespace FieldLink.Communication.Tests;

internal static partial class RobotProtocolTests
{
    internal static async Task SnapshotParserRejectsIncompleteInputAndPreservesOwnershipAsync()
    {
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(FanucSnapshotParser.Parse(null!, Encoding.ASCII)));
        await TestAssert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(FanucSnapshotParser.Parse(new byte[12260], null!)));
        foreach (int length in new[] { 0, 1, 100, 6129, 12259 })
            await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(FanucSnapshotParser.Parse(new byte[length], Encoding.ASCII)));
        byte[] content = new byte[12260];
        var first = FanucSnapshotParser.Parse(content, Encoding.ASCII).Content;
        var second = FanucSnapshotParser.Parse(content, Encoding.ASCII).Content;
        first.IntegerRegisters[0] = 1234;
        first.CurrentPose.Xyzwpr[0] = 12;
        TestAssert.Equal(0, second.IntegerRegisters[0]);
        TestAssert.Equal(0f, second.CurrentPose.Xyzwpr[0]);
        TestAssert.True(content.All(value => value == 0));
        for (int i = 0; i < content.Length; i++)
            content[i] = 0xFF;
        TestAssert.Equal(0, second.IntegerRegisters[0]);
        TestAssert.Equal(0f, second.CurrentPose.Xyzwpr[0]);
    }

    internal static Task SnapshotPropertyMappingIsCompleteAndUnambiguousAsync()
    {
        var properties = typeof(FanucControllerSnapshot).GetProperties();
        foreach (var mapping in SnapshotPropertyNames)
        {
            var property = properties.Single(p => p.Name == mapping.Key);
            TestAssert.True(!property.CanWrite || property.SetMethod?.IsPublic != true);
            TestAssert.True(properties.All(p => p.Name != mapping.Value));
        }
        TestAssert.Equal(34, SnapshotPropertyNames.Count);
        TestAssert.Equal(34, SnapshotPropertyNames.Values.Distinct().Count());
        TestAssert.True(typeof(FanucControllerSnapshot).GetConstructors().Length == 0);
        return Task.CompletedTask;
    }
}
