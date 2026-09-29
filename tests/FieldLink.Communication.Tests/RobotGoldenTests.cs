using System.Globalization;
using System.Collections;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.YASKAWA.Protocols;

namespace FieldLink.Communication.Tests;

internal static partial class RobotProtocolTests
{
    internal static Task RobotOriginalSourceGoldenVectorsAsync()
    {
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var document = JArray.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "robot-golden.json")));
            int count = 0;
            foreach (var vector in document)
            {
                string name = vector["Name"]!.Value<string>()!;
                string ownerName = vector["Destination"]!.Value<string>()!;
                var owner = typeof(OperationResult).Assembly.GetType(ownerName) ?? typeof(RobotGoldenAdapters).Assembly.GetType(ownerName)!;
                var inputs = vector["Arguments"]!.Children().ToArray();
                Type[] types = inputs.Select(input => Type.GetType(input["Type"]!.Value<string>()!, true)!).ToArray();
                object?[] arguments = inputs.Select((input, i) => input["Value"]!.ToObject(types[i])).ToArray();
                var method = owner.GetMethod(vector["Method"]!.Value<string>()!, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, types, null)!;
                object? actual;
                try
                {
                    actual = method.Invoke(method.IsStatic ? null : new YrcEthernetRequestBuilder(), arguments);
                    if (vector["Kind"]!.Value<string>() == "Ethernet")
                        actual = actual!.GetType().GetMethod("Build")!.Invoke(actual, [(byte)0x5a]);
                    actual = SnapshotRobot(actual);
                }
                catch (TargetInvocationException error) { actual = new { Exception = error.InnerException!.GetType().FullName }; }
                var expected = vector["Expected"]!.DeepClone();
                if (name == "FieldLink.Robot.YASKAWA.Protocols.YrcEthernetRequestBuilder.BuildJSeq#134")
                {
                    // 원본 fixture는 변경하지 않는다. R-006의 공식 Job 선택 필드 변경을 독립된 두 기대값으로 기록한다.
                    const string original = "59455243200024000301005A00000000393939393939393984000200011000004A4F4231000000000000000000000000000000000000000000000000000000007B000000";
                    const string corrected = "59455243200024000301005A00000000393939393939393987000100000200004A4F4231000000000000000000000000000000000000000000000000000000007B000000";
                    TestAssert.Equal(original, expected!.Value<string>());
                    expected = new JValue(corrected);
                }
                else if (name == "FieldLink.Robot.KUKA.Protocols.KukaVarProxyProtocol.ExtractActualData#58")
                {
                    // IMTS Answer Message Format requires a complete three-byte tail.
                    // https://github.com/ImtsSrl/KUKAVARPROXY#communicate-with-kukavarproxy
                    // Preserve the original fixture and verify both its old and corrected results.
                    TestAssert.Equal("AAEABQAAAUEB", inputs[0]["Value"]!.Value<string>());
                    TestAssert.True(JToken.DeepEquals(expected,
                        JObject.FromObject(new { Content = "41", ErrorCode = 0, IsSuccess = true })));
                    expected = JObject.FromObject(new { Content = (object?)null, ErrorCode = 10000, IsSuccess = false });
                }
                else if (name == "FieldLink.Robot.KUKA.Protocols.KukaVarProxyProtocol.ExtractActualData#59")
                {
                    // A lone zero byte has no response header or three-byte status tail.
                    // It is a local format failure (10000), not controller status byte zero.
                    TestAssert.Equal("AA==", inputs[0]["Value"]!.Value<string>());
                    TestAssert.True(JToken.DeepEquals(expected,
                        JObject.FromObject(new { Content = (object?)null, ErrorCode = 0, IsSuccess = false })));
                    expected = JObject.FromObject(new { Content = (object?)null, ErrorCode = 10000, IsSuccess = false });
                }
                else if (name == "FieldLink.Robot.YASKAWA.Protocols.YrcHighEthernetProtocol.CheckResponseContent#79")
                {
                    // Yaskawa HSES Packet Header is 32 bytes; reject the short frame as a result.
                    // https://knowledge.motoman.com/hc/en-us/articles/21740538183959
                    TestAssert.Equal("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==", inputs[0]["Value"]!.Value<string>());
                    TestAssert.True(JToken.DeepEquals(expected,
                        JObject.FromObject(new { Exception = "System.IndexOutOfRangeException" })));
                    expected = JObject.FromObject(new { ErrorCode = 10000, IsSuccess = false });
                }
                var snapshot = actual == null ? JValue.CreateNull() : JToken.FromObject(actual);
                TestAssert.True(JToken.DeepEquals(expected, snapshot), $"{name}\n원본: {expected}\n이식: {snapshot}");
                count++;
            }
            TestAssert.Equal(134, count);
        }
        finally { CultureInfo.CurrentCulture = savedCulture; }
        return Task.CompletedTask;
    }

    private static object? SnapshotRobot(object? value)
    {
        if (value == null)
            return null;
        if (value is byte[] bytes)
            return TestBytes.ToHexString(bytes);
        // 런타임별 소수 문자열 표기 차이를 허용하는 대신 IEEE 비트를 정확히 비교한다.
        if (value is float single)
            return new { SingleBits = TestBytes.ToHexString(BitConverter.GetBytes(single)) };
        if (value is double number)
            return new { DoubleBits = TestBytes.ToHexString(BitConverter.GetBytes(number)) };
        Type type = value.GetType();
        if (type.IsEnum)
            return Convert.ToInt64(value);
        if (type.IsPrimitive || value is string or decimal or DateTime)
            return value;
        if (value is IEnumerable items)
            return items.Cast<object?>().Select(SnapshotRobot).ToArray();
        var result = new SortedDictionary<string, object?>();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0);
        if (type.GetProperty("IsSuccess") != null)
            properties = properties.Where(p => new[] { "IsSuccess", "ErrorCode", "Content", "Content1", "Content2", "Content3" }.Contains(p.Name));
        foreach (var property in properties)
        {
            // Preserve all historical expected values; translate only the approved API property names.
            string name = type == typeof(FieldLink.Robot.FANUC.Protocols.FanucControllerSnapshot) &&
                SnapshotPropertyNames.TryGetValue(property.Name, out string? previous) ? previous : property.Name;
            result[name] = SnapshotRobot(property.GetValue(value));
        }
        return result;
    }

    private static readonly Dictionary<string, string> SnapshotPropertyNames = JObject.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "fanuc-member-migration.json")))
        .Properties().ToDictionary(p => p.Value.Value<string>()!, p => p.Name);
}

// 인코딩·변환기 같은 원본 전용 타입을 fixture 입력에서 분리하는 테스트 어댑터다.
internal static class RobotGoldenAdapters
{
    public static object FanucPose(byte[] data) => FieldLink.Robot.FANUC.Protocols.FanucPose.ParseFrom(new ProtocolValueConverter(), data, 0);
    public static object FanucTask(byte[] data) => FieldLink.Robot.FANUC.Protocols.FanucTask.ParseFrom(new ProtocolValueConverter(), data, 0, Encoding.ASCII);
    public static object FanucAlarm(byte[] data) => FieldLink.Robot.FANUC.Protocols.FanucAlarm.ParseFrom(new ProtocolValueConverter(), data, 0, Encoding.ASCII);
    public static object FanucData(byte[] data) => FieldLink.Robot.FANUC.Protocols.FanucSnapshotParser.Parse(data, Encoding.ASCII);
    public static object EstunData(byte[] data) => FieldLink.Robot.Estun.Protocols.EstunProtocol.ParseData(data);
    public static object HyundaiData(byte[] data) => new FieldLink.Robot.Hyundai.Protocols.HyundaiPositionCorrection(data);
    public static object YrcData(int type, string data) => new YrcRobotPosition((YrcControllerModel)type, data);
}
