using System.Collections;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Omron;
using FieldLink.PlcDrivers.Siemens;
using FieldLink.PlcDrivers.LSIS;
using FieldLink.PlcDrivers.AllenBradley;
using FieldLink.PlcDrivers.Beckhoff;
using FieldLink.PlcDrivers.Knx;
using FieldLink.PlcDrivers.Geniitek;
using FieldLink.PlcDrivers.Toledo;
using FieldLink.PlcDrivers.Sick;
using FieldLink.PlcDrivers.Keyence;
using FieldLink.PlcDrivers.Delta;

namespace FieldLink.Communication.Tests;

// 기대값은 수정하지 않은 참고 소스를 별도로 컴파일하여 생성한 고정 fixture다.
// 생성 경로·기준 커밋과 사례 범위는 docs/profinet-migration.md를 참조한다.
internal static class ProtocolMigrationTests
{
    internal static Task OriginalSourceGoldenVectorsAsync()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "profinet-golden.json");
        var document = JArray.Parse(File.ReadAllText(path));
        int count = 0;
        foreach (var vector in document)
        {
            AssertGoldenVector(vector);
            count++;
        }
        TestAssert.Equal(58, count);
        return Task.CompletedTask;
    }

    internal static void AssertGoldenVector(JToken vector)
    {
        string name = vector["Name"]!.Value<string>()!;
        Type owner = typeof(OperationResult).Assembly.GetType(vector["Destination"]!.Value<string>()!, true)!;
        var inputs = vector["Arguments"]!.Children().ToArray();
        Type[] parameterTypes = inputs.Select(input => Type.GetType(input["Type"]!.Value<string>()!, true)!).ToArray();
        MethodInfo method = owner.GetMethod(vector["Method"]!.Value<string>()!, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, parameterTypes, null)!;
        TestAssert.True(method != null, $"{name}: 이식된 메서드가 없습니다.");
        object?[] arguments = inputs.Select((input, i) => input["Value"]!.ToObject(parameterTypes[i])).ToArray();
        object? result;
        try { result = Snapshot(method!.Invoke(null, arguments)); }
        catch (TargetInvocationException error) { result = new { Exception = error.InnerException!.GetType().FullName }; }
        JToken? expected = vector["Expected"]!.DeepClone();
        JToken? actual = result == null ? JValue.CreateNull() : JToken.FromObject(result);
        TestAssert.True(JToken.DeepEquals(expected, actual), $"{name}\n원본: {expected}\n이식: {actual}");
    }

    internal static Task SourceConfigurationDefaultsAsync()
    {
        var mc = new McFrameOptions();
        TestAssert.Equal((byte)0xff, mc.PLCNumber);
        TestAssert.Equal((ushort)0x03ff, mc.TargetIOStation);
        TestAssert.True(new A3CFrameOptions().SumCheck);
        TestAssert.Equal(1, new A3CFrameOptions().Format);
        TestAssert.True(new FxLinksFrameOptions().SumCheck);
        TestAssert.Equal(1, new FxLinksFrameOptions().Format);
        TestAssert.True(!new FxGotFrameOptions().useGot);
        TestAssert.Equal((byte)1, new FinsTcpFrameOptions().SA1);
        TestAssert.Equal((byte)13, new FinsUdpFrameOptions().SA1);
        TestAssert.Equal((byte)0x30, new HostLinkFrameOptions().ResponseWaitTime);
        TestAssert.Equal(CheckType.CRC16, new Df1FrameOptions().CheckType);
        TestAssert.Equal(LSCpuInfo.XGK, new FastEnetFrameOptions().cpuInfo);
        TestAssert.Equal((byte)3, new FastEnetFrameOptions().slotNo);
        var s7Plus = new S7PlusCodecOptions();
        TestAssert.Equal((ushort)0x0600, s7Plus.LocalTSAP);
        TestAssert.Equal("SIMATIC-ROOT-HMI", Encoding.ASCII.GetString(s7Plus.destTSAP));
        TestAssert.Bytes([0, 0, 0, 0, 1, 1, 0x53, 3], new AdsFrameOptions().targetAMSNetId);
        return Task.CompletedTask;
    }

    internal static Task S7SessionFramesAsync()
    {
        // SiemensS7Net.plcHead1 및 Initialization(S300)의 고정 바이트.
        byte[] expected = TestBytes.FromHexString("0300001611E00000000100C0010AC1020102C2020102");
        TestAssert.Bytes(expected, S7SessionCodec.BuildConnection(SiemensPLCS.S300));
        byte[] customised = S7SessionCodec.BuildConnection(SiemensPLCS.S300, 0x23, 0x1000, 0x2001);
        TestAssert.Bytes([0x10, 0], customised.Skip(16).Take(2).ToArray());
        TestAssert.Bytes([0x20, 1], customised.Skip(20).Take(2).ToArray());
        // 실제 Setup ACK_DATA 외피와 협상값을 검증한다. 2바이트만 읽거나 최소 200으로 올리지 않는다.
        byte[] setup = ProtocolBytes.HexStringToBytes("0300001B02F080320300000001000800000000F0000001000101E0");
        TestAssert.Equal(452, S7SessionCodec.ParsePayloadLimit(setup));
        setup[25] = 0; setup[26] = 128;
        TestAssert.Equal(100, S7SessionCodec.ParsePayloadLimit(setup));
        byte[] stop = S7SessionCommandBuilder.BuildStop();
        stop[0] = 0;
        TestAssert.Equal((byte)3, S7SessionCommandBuilder.BuildStop()[0]);
        return Task.CompletedTask;
    }

    internal static Task KnxFramesAndCemiAsync()
    {
        // KnxCode.Knx_Resd_step1 / Knx_Write / Read_CEMI_29의 필드 위치.
        TestAssert.Bytes(TestBytes.FromHexString("061004200015040507001100BCE000001234010000"),
            KnxCommandBuilder.BuildRead(5, 7, 0x1234));
        TestAssert.Bytes(TestBytes.FromHexString("061004200015040507001100BCE000001234010081"),
            KnxCommandBuilder.BuildWrite(5, 7, 0x1234, 1, [1]));
        byte[] indication = TestBytes.FromHexString("061004200015040509002900BCE000001234010003");
        var parsed = KnxResponseParser.Parse(5, true, indication);
        TestAssert.Equal((short)0x1234, parsed.Address);
        TestAssert.Bytes([3, 0, 0, 0], parsed.Data);
        TestAssert.Bytes(TestBytes.FromHexString("06100421000A04050900"), parsed.Reply);
        TestAssert.Equal((short)0x1234, KnxAddressParser.ParseGroupAddress("2\\2\\52", out bool valid));
        TestAssert.True(valid);
        KnxAddressParser.ParseGroupAddress("32\\0\\0", out valid);
        TestAssert.True(!valid);
        return Task.CompletedTask;
    }

    internal static Task SensorScalingAndToledoFramingAsync()
    {
        // 짧은 Geniitek 프레임은 big-endian, 긴 피크 본문은 little-endian이다.
        var actual = VibrationSensorResponseParser.ParseActual([0xaa, 0, 100, 0xff, 0x9c, 0, 0, 0, 0]);
        TestAssert.Equal(1f, actual.AcceleratedSpeedX);
        TestAssert.Equal(-1f, actual.AcceleratedSpeedY);
        byte[] body = new byte[26];
        body[0] = 100; body[6] = 200; body[12] = 7; body[20] = 250; body[22] = 60;
        var peak = VibrationSensorResponseParser.ParsePeak(body);
        TestAssert.Equal(1f, peak.AcceleratedSpeedX);
        TestAssert.Equal(2f, peak.SpeedX);
        TestAssert.Equal(7f, peak.OffsetX);
        TestAssert.Equal(2.5f, peak.Voltage);
        TestAssert.Equal(60, peak.SendingInterval);
        byte[] frame = Encoding.ASCII.GetBytes("\u0002\u0004\0\0" + "001234" + "000100" + "\r");
        var scale = new ToledoStandardData(frame);
        TestAssert.Equal(12.34f, scale.Weight);
        TestAssert.Equal(1f, scale.Tare);
        TestAssert.True(ToledoFrameParser.IsComplete(frame, frame.Length, false));
        TestAssert.True(!ToledoFrameParser.IsComplete(frame, frame.Length, true));
        byte[] checkedFrame = [..frame, 0x55];
        TestAssert.True(ToledoFrameParser.IsComplete(checkedFrame, checkedFrame.Length, true));
        TestAssert.True(!ToledoFrameParser.IsComplete(frame, 15, false));
        return Task.CompletedTask;
    }

    internal static Task ScannerParsingAndKoreanErrorsAsync()
    {
        TestAssert.Equal("ABC123", SickIcrTcpServerValueConverter.TranslateCode("\u0002ABC-123\r\n"));
        TestAssert.Bytes(Encoding.ASCII.GetBytes("LON\r"), Sr2000CommandBuilder.BuildReadBarcode());
        var success = Sr2000ResponseParser.Parse("INCHK,1", Encoding.ASCII.GetBytes("OK,INCHK,ON\r"));
        TestAssert.True(success.IsSuccess);
        TestAssert.Equal("ON", success.Content);
        TestAssert.True(Sr2000ResponseParser.ParseInput(success.Content).Content);
        var failure = Sr2000ResponseParser.Parse("LON", Encoding.ASCII.GetBytes("ER,LON,01\r"));
        TestAssert.True(!failure.IsSuccess);
        TestAssert.True(failure.Message.Contains("매개변수"));
        var tooShort = SiemensS7ResponseParser.AnalysisReadBit([0]);
        TestAssert.True(!tooShort.IsSuccess);
        TestAssert.True(tooShort.Message.Contains("길이"));
        return Task.CompletedTask;
    }

    internal static Task FrameLengthAndIdentityAsync()
    {
        byte[] s7Header = [3, 0, 0, 25];
        var s7 = new S7FrameRules();
        TestAssert.Equal(21, s7.GetBodyLength(s7Header));
        byte[] cipHeader = new byte[24];
        var cip = new EtherNetIpFrameRules();
        cipHeader[2] = 0x34; cipHeader[3] = 0x12;
        TestAssert.Equal(0x1234, cip.GetBodyLength(cipHeader));
        byte[] mcHeader = [0xd0, 0, 0, 0xff, 0xff, 3, 0, 4, 0];
        var mc = new MelsecQnA3EBinaryFrameRules();
        TestAssert.Equal(4, mc.GetBodyLength(mcHeader));
        return Task.CompletedTask;
    }

    internal static Task PlcStringMemoryLayoutsAsync()
    {
        // 원본 Write 구현의 헤더, 패딩, 워드 순서를 명시한 기대 바이트.
        TestAssert.Bytes([10, 2, 65, 66], S7StringCodec.BuildString(SiemensPLCS.S1200, [10, 0], "AB", Encoding.ASCII).Content);
        // 최대 길이 0을 254로 확장하던 기존 동작은 선언된 PLC 용량을 침범하므로 거부한다.
        TestAssert.True(!S7StringCodec.BuildString(SiemensPLCS.S1200, [0, 0], "A", Encoding.ASCII).IsSuccess);
        TestAssert.True(!S7StringCodec.BuildString(SiemensPLCS.S1200, [1, 0], "AB", Encoding.ASCII).IsSuccess);
        TestAssert.Bytes([0, 10, 0, 1, 0xac, 0], S7StringCodec.BuildWideString(SiemensPLCS.S1200, [0, 10, 0, 0], "가").Content);
        TestAssert.Bytes([2, 0xac, 0], S7StringCodec.BuildWideString(SiemensPLCS.S200Smart, [], "가").Content);
        TestAssert.Equal("가", S7StringCodec.ParseWideString(SiemensPLCS.S1200, [0, 10, 0, 1, 0xac, 0]));
        TestAssert.Bytes([3, 0, 66, 65, 0, 67], PcccStringCodec.Build("ABC", Encoding.ASCII, new ProtocolValueConverter()));
        TestAssert.Equal("ABC", PcccStringCodec.Parse([3, 0, 66, 65, 0, 67], Encoding.ASCII, true));
        TestAssert.Bytes([3, 0, 65, 66, 67], OmronCipValueCodec.BuildConnectedString("ABC", Encoding.ASCII));
        TestAssert.Bytes([4, 0, 65, 66, 67, 0], OmronUnconnectedStringCodec.Build("ABC", Encoding.ASCII));
        TestAssert.True(!OmronUnconnectedStringCodec.Parse([4, 0, 65], Encoding.ASCII, new ProtocolValueConverter()).IsSuccess);
        var logix = LogixStringCodec.Build("ABC", Encoding.ASCII);
        TestAssert.Bytes([3, 0, 0, 0], logix.Content1);
        TestAssert.Bytes([65, 66, 67, 0], logix.Content2);
        return Task.CompletedTask;
    }

    internal static Task CipDateTimeAndJsonMetadataAsync()
    {
        var transform = new ProtocolValueConverter();
        var date = new DateTime(1970, 1, 2, 0, 0, 1);
        TestAssert.Equal(86400000000000L, BitConverter.ToInt64(CipDateTimeCodec.BuildDate(date, transform), 0));
        TestAssert.Equal(86401000000000L, BitConverter.ToInt64(CipDateTimeCodec.BuildTimeAndDate(date, transform), 0));
        TestAssert.Equal(100L, BitConverter.ToInt64(CipDateTimeCodec.BuildTime(TimeSpan.FromTicks(1), transform), 0));
        TestAssert.Equal(date, CipDateTimeCodec.ParseDate(86401000000000L));
        TestAssert.Equal(TimeSpan.FromTicks(1), CipDateTimeCodec.ParseTime(199));
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(new ToledoStandardData { SourceData = [1, 2] });
        TestAssert.True(!json.Contains("SourceData"), "원본 JsonIgnore 계약을 유지해야 합니다.");
        return Task.CompletedTask;
    }

    internal static Task SessionAddressAndForwardOpenAsync()
    {
        TestAssert.Bytes(TestBytes.FromHexString("46494E530000000C000000000000000000000000"), FinsSessionCommandBuilder.BuildNodeAddressRequest());
        var options = new FinsTcpFrameOptions();
        byte[] fins = new byte[24];
        fins[19] = 17; fins[23] = 29;
        TestAssert.True(FinsSessionResponseParser.ParseNodeAddresses(options, fins).IsSuccess);
        TestAssert.Equal((byte)17, options.SA1);
        TestAssert.Equal((byte)29, options.DA1);
        fins[15] = 3;
        TestAssert.Equal(3, FinsSessionResponseParser.ParseNodeAddresses(options, fins).ErrorCode);
        byte[] cip = new byte[48];
        cip[4] = 0x78; cip[5] = 0x56; cip[6] = 0x34; cip[7] = 0x12;
        TestAssert.Equal(0x12345678u, CipSessionResponseParser.ParseRegisterSession(cip).Content);
        cip[44] = 0x11; cip[45] = 0x22; cip[46] = 0x33; cip[47] = 0x44;
        TestAssert.Equal(0x44332211u, CipForwardOpenResponseParser.Parse(cip, new ProtocolValueConverter()).Content);
        cip[42] = 1; cip[44] = 0; cip[45] = 1;
        var duplicate = CipForwardOpenResponseParser.Parse(cip, new ProtocolValueConverter());
        TestAssert.True(!duplicate.IsSuccess && duplicate.Message.Contains("중복"));
        TestAssert.Bytes(TestBytes.FromHexString("001002FFFFFC011003"), FxGotSessionCommandBuilder.BuildInitializationRequests()[0]);
        TestAssert.Bytes([0x02, 0x41, 0x32, 0x03, 0x37, 0x36], FxSerialSessionCommandBuilder.BuildBaudRateChange(38400));
        TestAssert.Bytes([0x05], FxSerialSessionCommandBuilder.BuildEnquiry());
        TestAssert.True(FxSerialActivationCommandBuilder.CheckActivationAck([0x06]).IsSuccess);
        return Task.CompletedTask;
    }

    internal static Task DeltaDvpBoundaryRequestsAsync()
    {
        // DeltaDvpHelper.Read/Write의 M1536·D4096 교차 분기에서 가져온 기대값.
        var bits = DeltaDvpRequestPlanner.SplitReadBits("s=2;M1535", 3);
        TestAssert.Equal(2, bits.Length);
        TestAssert.Equal("s=2;M1535", bits[0].Address);
        TestAssert.Equal(1, bits[0].Length);
        TestAssert.Equal("s=2;M1536", bits[1].Address);
        TestAssert.Equal(2, bits[1].Length);
        var words = DeltaDvpRequestPlanner.SplitWriteWords("D4095", 5);
        TestAssert.Equal(2, words[0].Length);
        TestAssert.Equal("D4096", words[1].Address);
        TestAssert.Equal(2, words[1].Offset);
        TestAssert.Equal(3, words[1].Length);
        // 원본 기대값은 "D100"이었다. R-014 수정으로 지정 국번을 보존하는 의도적 차이를 검사한다.
        TestAssert.Equal("s=2;D100", DeltaDvpRequestPlanner.SplitReadWords("s=2;D100", 2)[0].Address);
        TestAssert.Equal(1, DeltaDvpRequestPlanner.SplitReadWords("D4095", 1).Length);
        TestAssert.Equal(1, DeltaDvpRequestPlanner.SplitReadBits("m1535", 3).Length);
        TestAssert.True(!DeltaAddressParser.TranslateToModbusAddress((DeltaSeries)99, "D100", 3).IsSuccess);
        return Task.CompletedTask;
    }

    private static object? Snapshot(object? value)
    {
        if (value == null)
            return null;
        if (value is byte[] bytes)
            return TestBytes.ToHexString(bytes);
        Type type = value.GetType();
        if (type.IsEnum)
            return Convert.ToInt64(value);
        if (type.IsPrimitive || value is string or decimal or DateTime)
            return value;
        if (value is IEnumerable items)
            return items.Cast<object?>().Select(Snapshot).ToArray();
        var result = new SortedDictionary<string, object?>();
        if (type.GetProperty("IsSuccess") != null)
        {
            foreach (string name in new[] { "IsSuccess", "ErrorCode", "Content", "Content1", "Content2", "Content3" })
            {
                var property = type.GetProperty(name);
                if (property != null)
                    result[name] = Snapshot(property.GetValue(value));
            }
        }
        else
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                result[property.Name] = Snapshot(property.GetValue(value));
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) result[field.Name] = Snapshot(field.GetValue(value));
        }
        return result;
    }
}
