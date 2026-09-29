using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

// 이식 코드를 참조하지 않고, 별도로 컴파일한 참고 소스의 순수 메서드만 호출한다.
internal static class RobotGolden
{
    static Assembly baseline;
    static readonly List<object> vectors = new List<object>();
    static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Type SourceType(string name) => baseline.GetTypes().Single(t => t.Name == name);
    static object Call(string type, string method, params object[] args)
    {
        Type owner = SourceType(type);
        MethodInfo member = owner.GetMethods(Flags).Single(m => m.Name == method && m.GetParameters().Length == args.Length && m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x));
        // ABB에서는 필드에 의존하지 않는 파서만 호출하므로 HTTP 객체 생성도 생략한다.
        object instance = member.IsStatic ? null : type == "ABBWebApiClient" ? System.Runtime.Serialization.FormatterServices.GetUninitializedObject(owner) : Activator.CreateInstance(owner);
        return member.Invoke(instance, args);
    }
    static object Snapshot(object value)
    {
        if (value == null)
            return null;
        if (value is byte[] bytes)
            return BitConverter.ToString(bytes).Replace("-", "");
        if (value is float single)
            return new
            {
                SingleBits = BitConverter.ToString(BitConverter.GetBytes(single)).Replace("-", "")
            };
        if (value is double number)
            return new
            {
                DoubleBits = BitConverter.ToString(BitConverter.GetBytes(number)).Replace("-", "")
            };
        Type type = value.GetType();
        if (type.IsEnum)
            return Convert.ToInt64(value);
        if (type.IsPrimitive || value is string || value is decimal || value is DateTime)
            return value;
        if (value is IEnumerable values)
            return values.Cast<object>().Select(Snapshot).ToArray();
        var result = new SortedDictionary<string, object>();
        if (type.GetProperty("IsSuccess") != null)
        {
            foreach (string name in new[]
            {
                "IsSuccess",
                "ErrorCode",
                "Content",
                "Content1",
                "Content2",
                "Content3"
            }

            )
            {
                var p = type.GetProperty(name);
                if (p != null)
                    result[name] = Snapshot(p.GetValue(value));
            }
        }
        else
            foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                result[p.Name] = Snapshot(p.GetValue(value));
        return result;
    }
    static void Add(string destination, string method, object[] inputs, Func<object> source, string kind = "Static")
    {
        object result;
        try { result = Snapshot(source()); }
        catch (TargetInvocationException error) { result = new { Exception = error.InnerException.GetType().FullName }; }
        vectors.Add(new { Name = destination + "." + method + "#" + (vectors.Count + 1), Destination = destination, Method = method, Kind = kind,
            Arguments = inputs.Select(value => new { Type = value == null ? "System.Byte[]" : value.GetType().FullName, Value = value }).ToArray(), Expected = result });
    }
    static void Pure(string company, string source, string destination, string method, string target, params object[] inputs)
        => Add("FieldLink.Robot." + company + ".Protocols." + destination, target, inputs, () => Call(source, method, inputs));
    static void Ethernet(string method, object[] inputs, ushort command, ushort address, byte attribute, byte service, byte[] data)
        => Add("FieldLink.Robot.YASKAWA.Protocols.YrcEthernetRequestBuilder", method, inputs,
            () => Call("YRCHighEthernetHelper", "BuildCommand", (byte)1, (byte)0x5A, command, address, attribute, service, data), "Ethernet");
    static byte[] Pattern(int length) => Enumerable.Range(0, length).Select(i => (byte)(i % 17)).ToArray();
    static int Main(string[] args)
    {
        try { Generate(args); return 0; }
        catch (Exception error)
        {
            Console.Error.WriteLine("기준 데이터 생성 실패: " + error.GetType().Name + " " + error.Message);
            if (error.InnerException != null)
                Console.Error.WriteLine(error.InnerException.GetType().Name + " " + error.InnerException.Message);
            return 1;
        }
    }
    static void Generate(string[] args)
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        baseline = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "ProtocolReferenceBaseline.dll"));
        foreach (string address in new[]
        {
            "D1",
            "AQ65535",
            "SR1",
            "SR6",
            "SR7",
            "R10",
            "R11",
            "GO10001",
            "bad",
            "SDO11000",
            "SDO11001",
            "RDI100",
            "RDO1",
            "UI1",
            "UO1",
            "SI1",
            "SO1"
        }

        )
            foreach (bool bits in new[]
            {
                false,
                true
            }

            )
                Pure("FANUC", "FanucHelper", "FanucProtocol", "AnalysisFanucAddress", "ParseAddress", address, bits);
        Pure("FANUC", "FanucHelper", "FanucProtocol", "BulidReadData", "BuildReadData", (byte)8, (ushort)100, (ushort)50);
        Pure("FANUC", "FanucHelper", "FanucProtocol", "BulidReadData", "BuildReadData", (byte)72, (ushort)0, (ushort)0);
        foreach (int length in new[]
        {
            0,
            1,
            6,
            7,
            100
        }

        )
        {
            Pure("FANUC", "FanucHelper", "FanucProtocol", "BuildWriteData", "BuildWriteData", (byte)8, (ushort)100, Pattern(length), length / 2);
            Pure("FANUC", "FanucHelper", "FanucProtocol", "BuildReadResponseData", "BuildReadResponseData", Pattern(length));
        }

        Pure("FANUC", "FanucHelper", "FanucProtocol", "GetFanucCmds", "GetAssignmentCommands");
        foreach (bool old in new[]
        {
            false,
            true
        }

        )
            foreach (int size in new[]
            {
                old ? 783 : 787,
                old ? 784 : 788
            }

            )
                Pure("EFORT", "EfortData", "EfortData", old ? "PraseFromPrevious" : "PraseFrom", old ? "ParseFromPrevious" : "ParseFrom", Pattern(size));
        foreach (string[] names in new[]
        {
            new string[0],
            new[]
            {
                "A",
                "B"
            }
        }

        )
            Pure("KUKA", "KukaTcpNet", "KukaTextProtocol", "BuildReadCommands", "BuildReadCommands", (object)names);
        Pure("KUKA", "KukaTcpNet", "KukaTextProtocol", "BuildWriteCommands", "BuildWriteCommands", new[] { "A", "B" }, new[] { "1", "2" });
        Pure("KUKA", "KukaTcpNet", "KukaTextProtocol", "BuildWriteCommands", "BuildWriteCommands", new[] { "A", "B" }, new[] { "1" });
        Pure("KUKA", "KukaAvarProxyNet", "KukaVarProxyProtocol", "BuildReadValueCommand", "BuildReadValueCommand", "$AXIS_ACT");
        Pure("KUKA", "KukaAvarProxyNet", "KukaVarProxyProtocol", "BuildWriteValueCommand", "BuildWriteValueCommand", "$OV_PRO", "20");
        foreach (byte[] response in new[]
        {
            new byte[]
            {
                0,
                1,
                0,
                5,
                0,
                0,
                1,
                65,
                1
            },
            new byte[]
            {
                0
            },
            new byte[0]
        }

        )
            Pure("KUKA", "KukaAvarProxyNet", "KukaVarProxyProtocol", "ExtractActualData", "ExtractActualData", response);
        foreach (string value in new[]
        {
            "OK",
            "OKAY",
            "NG"
        }

        )
            Pure("YAMAHA", "YamahaRCX", "YamahaRcxProtocol", "CheckResponseOk", "CheckResponseOk", value);
        foreach (string value in new[]
        {
            "<span class=\"value\">12.5</span>",
            "<body/>",
            "<span class=\"value\">1</span><span class=\"value\">2</span>"
        }

        )
        {
            Pure("ABB", "ABBWebApiClient", "AbbResponseParser", "ParseSpanByClass", "ParseSpanByClass", value, "value");
            Pure("ABB", "ABBWebApiClient", "AbbResponseParser", "ParseDoubleListSpanByClass", "ParseDoubleListSpanByClass", value, "value");
        }

        string html = "<li class=\"item\"><span class=\"name\">A</span></li><li class=\"item\"><span class=\"name\">B</span></li>";
        Pure("ABB", "ABBWebApiClient", "AbbResponseParser", "ParseJObjectByClass", "ParseJObjectByClass", html, "item");
        Pure("ABB", "ABBWebApiClient", "AbbResponseParser", "ParseJArrayByClass", "ParseJArrayByClass", html, "item", 1);
        Pure("ABB", "ABBWebApiClient", "AbbResponseParser", "ParseJObjectByClass", "ParseJObjectByClass", "<li class=\"item\"><span>A</span></li>", "item");
        foreach (byte status in new byte[]
        {
            0,
            8,
            9,
            0x1f,
            0x28,
            0xff
        }

        )
        {
            byte[] response = new byte[32];
            response[25] = status;
            response[26] = 2;
            response[28] = 0x10;
            response[29] = 0x20;
            Pure("YASKAWA", "YRCHighEthernetHelper", "YrcHighEthernetProtocol", "CheckResponseContent", "CheckResponseContent", response);
        }

        Pure("YASKAWA", "YRCHighEthernetHelper", "YrcHighEthernetProtocol", "CheckResponseContent", "CheckResponseContent", new byte[25]);
        object transform = Activator.CreateInstance(SourceType("RegularByteTransform"));
        foreach (string model in new[]
        {
            "FanucPose",
            "FanucTask",
            "FanucAlarm"
        }

        )
        {
            byte[] data = Pattern(model == "FanucPose" ? 100 : model == "FanucTask" ? 36 : 200);
            if (model == "FanucAlarm")
                Array.Clear(data, 10, 12);
            Add("FieldLink.Communication.Tests.RobotGoldenAdapters", model, new object[] { data }, () => model == "FanucPose" ? Call(model, "ParseFrom", transform, data, 0) : Call(model, model == "FanucAlarm" ? "PraseFrom" : "ParseFrom", transform, data, 0, Encoding.ASCII));
        }

        byte[] full = new byte[12260];
        full[6544] = 42;
        full[6900] = 9;
        Encoding.ASCII.GetBytes("JOB_A").CopyTo(full, 2000);
        Add("FieldLink.Communication.Tests.RobotGoldenAdapters", "FanucData", new object[] { full }, () => Call("FanucData", "PraseFrom", full, Encoding.ASCII));
        byte[] estun = Pattern(200);
        object cdab = Activator.CreateInstance(SourceType("RegularByteTransform"), Enum.Parse(SourceType("DataFormat"), "CDAB"));
        Add("FieldLink.Communication.Tests.RobotGoldenAdapters", "EstunData", new object[] { estun }, () => Activator.CreateInstance(SourceType("EstunData"), estun, cdab));
        byte[] hyundai = new byte[64];
        hyundai[0] = 80;
        hyundai[4] = 2;
        hyundai[8] = 3;
        BitConverter.GetBytes(1.25d).CopyTo(hyundai, 16);
        BitConverter.GetBytes(Math.PI).CopyTo(hyundai, 40);
        Add("FieldLink.Communication.Tests.RobotGoldenAdapters", "HyundaiData", new object[] { hyundai }, () => Activator.CreateInstance(SourceType("HyundaiData"), (object)hyundai));
        foreach (int type in new[]
        {
            0,
            1
        }

        )
        {
            string text = type == 0 ? "1,2,3,4,5,6,5,7,8,9,10,11,12,13" : "1,2,3,4,5,6,7,5,8,9,10,11,12,13,14";
            Add("FieldLink.Communication.Tests.RobotGoldenAdapters", "YrcData", new object[] { type, text }, () => Activator.CreateInstance(SourceType("YRCRobotData"), Enum.ToObject(SourceType("YRCType"), type), text));
        }

        Ethernet("BuildReadStats", new object[0], 0x72, 1, 0, 1, null);
        Ethernet("BuildReadJSeq", new object[] { (ushort)2 }, 0x73, 2, 0, 1, null);
        Ethernet("BuildReadPose", new object[0], 0x75, 101, 0, 1, null);
        Ethernet("BuildReadTorqueData", new object[0], 0x77, 21, 0, 1, null);
        string[] variableNames =
        {
            "IO",
            "RegisterVariable",
            "ByteVariable",
            "IntegerVariable",
            "DoubleIntegerVariable",
            "RealVariable",
            "StringVariable"
        };
        object[] singles =
        {
            (byte)5,
            (ushort)0x1234,
            (byte)6,
            (short)-7,
            123456,
            1.5f,
            "HELLO"
        };
        object[] arrays =
        {
            new byte[]
            {
                5,
                6
            },
            new ushort[]
            {
                0x1234,
                0x5678
            },
            new byte[]
            {
                7,
                8
            },
            new short[]
            {
                -7,
                8
            },
            new int[]
            {
                123456,
                -7
            },
            new float[]
            {
                1.5f,
                -2
            },
            new[]
            {
                "HELLO",
                "WORLD"
            }
        };
        for (int i = 0; i < variableNames.Length; i++)
        {
            string name = variableNames[i];
            Ethernet("BuildRead" + name, new object[] { (ushort)10 }, (ushort)(0x78 + i), 10, 1, 0x0e, null);
            Ethernet("BuildRead" + name, new object[] { (ushort)10, 2 }, (ushort)(0x300 + i), 10, 0, 0x33, BitConverter.GetBytes(2));
            Func<object, byte[]> bytes = value =>
            {
                if (value is byte b)
                    return new[]
                    {
                        b
                    };
                if (value is byte[] bs)
                    return bs;
                if (value is string text)
                {
                    byte[] result = new byte[16];
                    Encoding.ASCII.GetBytes(text).CopyTo(result, 0);
                    return result;
                }

                if (value is string[] texts)
                {
                    byte[] result = new byte[texts.Length * 16];
                    for (int j = 0; j < texts.Length; j++)
                        Encoding.ASCII.GetBytes(texts[j]).CopyTo(result, j * 16);
                    return result;
                }

                return (byte[])transform.GetType().GetMethods().Single(m => m.Name == "TransByte" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == value.GetType()).Invoke(transform, new[] { value });
            };
            Ethernet("BuildWrite" + name, new object[] { (ushort)10, singles[i] }, (ushort)(0x78 + i), 10, 1, 0x10, bytes(singles[i]));
            Ethernet("BuildWrite" + name, new object[] { (ushort)10, arrays[i] }, (ushort)(0x300 + i), 10, 0, 0x34, bytes(arrays[i]));
        }

        foreach (bool on in new[]
        {
            false,
            true
        }

        )
            foreach (var item in new[]
            {
                Tuple.Create("BuildHold", (ushort)1),
                Tuple.Create("BuildSvon", (ushort)2),
                Tuple.Create("BuildHLock", (ushort)3)
            }

            )
                Ethernet(item.Item1, new object[] { on }, 0x83, item.Item2, 1, 0x10, BitConverter.GetBytes(on ? 1 : 2));
        Ethernet("BuildReset", new object[0], 0x82, 1, 1, 0x10, BitConverter.GetBytes(1));
        Ethernet("BuildCancel", new object[0], 0x82, 2, 1, 0x10, BitConverter.GetBytes(1));
        Ethernet("BuildCycle", new object[] { 3 }, 0x84, 2, 1, 0x10, BitConverter.GetBytes(3));
        Ethernet("BuildMSDP", new object[] { "READY" }, 0x85, 1, 1, 0x10, Encoding.ASCII.GetBytes("READY"));
        Ethernet("BuildStart", new object[0], 0x86, 1, 1, 0x10, BitConverter.GetBytes(1));
        Ethernet("BuildReadManagementTime", new object[] { (ushort)1 }, 0x88, 1, 1, 0x0e, null);
        Ethernet("BuildReadManagementTimeSpan", new object[] { (ushort)1 }, 0x88, 1, 2, 0x0e, null);
        Ethernet("BuildReadSystemInfo", new object[] { (ushort)11 }, 0x89, 11, 0, 1, null);
        byte[] job = new byte[36];
        Encoding.ASCII.GetBytes("JOB1").CopyTo(job, 0);
        BitConverter.GetBytes(123).CopyTo(job, 32);
        Ethernet("BuildJSeq", new object[] { "JOB1", 123 }, 0x84, 2, 1, 0x10, job);
        File.WriteAllText(args[1], JsonConvert.SerializeObject(vectors, Formatting.Indented));
        Console.WriteLine("Robot golden vectors: " + vectors.Count);
    }
}
