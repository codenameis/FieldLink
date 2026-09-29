using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json; using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
partial class Golden {
    static Assembly baseline;
    static JToken[] map;
    static string root;
    static List<object> cases=new();
    static BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    static void Add(string owner,string method, params object[] args) {
        var matches=map.Where(r=>r.GetProperty("type").GetString()==owner&&r.GetProperty("members").EnumerateArray().Any(v=>v.GetString()==method)).ToArray();
        var row=matches.First();
        string file=row.GetProperty("source").GetString();
        string ns=Regex.Match(File.ReadAllText(Path.Combine(root,file)),@"namespace\s+([\w.]+)").Groups[1].Value;
        var t=baseline.GetType(ns+"."+owner,true);
        var methodInfo=t.GetMethods(flags).Single(m=>m.Name==method&&m.GetParameters().Length==args.Length&&m.GetParameters().Select((p,i)=>args[i]==null||p.ParameterType.IsInstanceOfType(args[i])).All(b=>b));
        var parameters=methodInfo.GetParameters();
        string destination=row.GetProperty("destination").GetString();
        string dest="FieldLink.Profinet."+destination.Replace('/','.').Replace(".cs","");
        var inputs=args.Select((a,i)=>new {Type=parameters[i].ParameterType.FullName,Value=a}).ToArray();
        object result;
        try {result=Snapshot(methodInfo.Invoke(null,args));}
        catch(TargetInvocationException e){result=new {Exception=e.InnerException.GetType().FullName};}
        cases.Add(new {Name=owner+"."+method+"#"+(cases.Count+1),Source=file,Destination=dest,Method=method,Arguments=inputs,Expected=result});
    }
    static object Snapshot(object value)
    {
        if (value == null)
            return null;
        if (value is byte[] bytes)
            return BitConverter.ToString(bytes).Replace("-", "");
        Type t = value.GetType();
        if (t.IsEnum)
            return Convert.ToInt64(value);
        if (t.IsPrimitive || value is string || value is decimal || value is DateTime)
            return value;
        if (value is IEnumerable items)
            return items.Cast<object>().Select(Snapshot).ToArray();
        var result = new SortedDictionary<string, object>();
        if (t.GetProperty("IsSuccess") != null)
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
                var p = t.GetProperty(name);
                if (p != null)
                    result[name] = Snapshot(p.GetValue(value));
            }
        }
        else
        {
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                result[p.Name] = Snapshot(p.GetValue(value));
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                result[f.Name] = Snapshot(f.GetValue(value));
        }

        return result;
    }
    static void Main(string[] args) {
        root=args[0];baseline=Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"ProtocolReferenceBaseline.dll"));
        map=JArray.Parse(File.ReadAllText(args[1])).ToArray();
        Add("AllenBradleyHelper","RegisterSessionHandle",new byte[4]);
        Add("AllenBradleyHelper","PackRequestHeader",(ushort)0x6f,(uint)0x12345678,new byte[]{1,2,3},new byte[8]);
        Add("AllenBradleySLCNet","BuildReadCommand","N7:0",(ushort)2);
        Add("AllenBradleySLCNet","BuildReadCommand","Z1:0",(ushort)2);
        Add("AdsHelper","BuildReadDeviceInfoCommand"); Add("AdsHelper","BuildReadStateCommand");
        Add("AdsHelper","BuildReadCommand","M100",4,false); Add("AdsHelper","BuildReadCommand","invalid",4,false);
        Add("DeltaDvpHelper","ParseDeltaDvpAddress","D4096",(byte)3); Add("DeltaDvpHelper","ParseDeltaDvpAddress","X17",(byte)1);
        Add("DeltaASHelper","ParseDeltaASAddress","M100",(byte)1);
        Add("FatekProgramHelper","BuildReadWordCommand",(byte)1,"D100",(ushort)2);
        Add("FatekProgramHelper","BuildReadWordCommand",(byte)1,"D100",(ushort)300);
        Add("FatekProgramHelper","BuildWriteBoolCommand",(byte)2,"M10",new bool[]{true,false,true});
        Add("FujiCommandSettingType","BuildReadCommand","D100",(ushort)2);
        Add("FujiSPBHelper","BuildReadCommand",(byte)1,"D100",(ushort)2);
        Add("FujiSPHNet","BuildReadCommand",(byte)0xfe,"M1.100",(ushort)2);
        Add("GeHelper","BuildReadCommand",(long)0x1234,"R1",(ushort)2,false);
        Add("GeHelper","BuildReadCommand",(long)1,"R0",(ushort)2,false);
        Add("VibrationSensorClient","BulidLongMessage",(ushort)0x1234,(byte)1,null);
        Add("SAMSerial","BuildReadCommand",(byte)0x20,(byte)1,null);
        Add("SAMSerial","PackToSAMCommand",new byte[]{0x20,1});
        Add("KeyenceNanoHelper","BuildReadCommand","DM100",(ushort)2);
        Add("KeyenceNanoHelper","BuildWriteCommand","DM100",new byte[]{0x34,0x12,0x78,0x56});
        Add("LSCnetHelper","BuildReadByteCommand",(byte)1,"D100",(ushort)2);
        Add("LSCpuHelper","BuildReadByteCommand",(byte)1,"D100",(ushort)2);
        Add("LSFastEnet","BuildReadIndividualCommand",(byte)0,"D100");
        Add("MelsecA1ENet","BuildReadCommand","D100",(ushort)2,false,(byte)0xff);
        Add("MelsecA1EAsciiNet","BuildReadCommand","D100",(ushort)2,false,(byte)0xff);
        Add("MelsecFxLinksHelper","BuildReadCommand",(byte)1,"D100",(ushort)2,false,(byte)0);
        Add("MelsecFxSerialHelper","BuildReadWordCommand","D100",(ushort)2,true);
        Add("MelsecFxSerialHelper","BuildReadBoolCommand","M100",(ushort)17,false);
        Add("MelsecFxSerialHelper","BuildWriteBoolPacket","M100",true);
        Add("OmronHostLinkCModeHelper","BuildReadCommand","D100",(ushort)2,false);
        Add("OpenProtocolNet","BuildReadCommand",1,1,-1,-1,null);
        Add("OpenProtocolNet","BuildReadCommand",18,1,2,3,new List<string>{"001"});
        Add("PanasonicHelper","BuildReadOneCoil",(byte)1,"R100");
        Add("SiemensPPIHelper","BuildReadCommand",(byte)2,"DB1.0",(ushort)2,false);
        Add("SiemensFetchWriteNet","BuildReadCommand","DB1.0",(ushort)2);
        Add("SiemensMPI","BuildReadCommand",(byte)2,"M100",(ushort)2,false);
        Add("ToyoPuc","BuildReadWordCommand","D100",(ushort)2);
        Add("ReaderNet","BuildReadCommand",(byte)1,(byte)3,(byte)4);
        Add("VigorVsHelper","BuildReadCommand",(byte)1,"D100",(ushort)2,false);
        Add("XinJEHelper","BuildReadCommand",(byte)1,"D100",(ushort)2,false);
        Add("DigitronCPLHelper","BuildReadCommand",(byte)1,"100",(ushort)2);
        Add("MemobusHelper","BuildReadCommand",(byte)0x20,(byte)3,(byte)1,(byte)0,(ushort)100,(ushort)2);
        Add("YokogawaLinkTcp","BuildReadCommand",(byte)1,"D100",(ushort)2,false);
        Add("YokogawaLinkTcp","BuildReadCommand",(byte)1,"D100",(ushort)1000,false);
        Add("MelsecHelper","TransBoolArrayToByteData",new bool[]{true,false,true});
        Add("MelsecHelper","FxCalculateCRC",new byte[]{2,0x30,0x31,3},1,0);
        Add("SiemensS7Helper","AnalysisReadBit",new byte[5]);
        Add("OmronFinsNetHelper","UdpResponseValidAnalysis",new byte[]{0xc0,0,2,0,0,0,0,0,0,0,1,1,0,0,0x12,0x34});
        Add("FujiSPBHelper","CheckResponseData",Encoding.ASCII.GetBytes(":010500000000\r\n"));
        Add("InovanceHelper","PraseInovanceAMAddress","M100",(byte)1);
        Add("InovanceHelper","PraseInovanceH3UAddress","D100",(byte)3);
        Add("InovanceHelper","PraseInovanceH5UAddress","X17",(byte)1);
        Add("MegMeetHelper","PraseMegMeetAddress","D100",(byte)3);
        Add("MegMeetHelper","PraseMegMeetAddress","X17",(byte)1);
        // 모든 사례가 생성된 뒤 출력하여 쓰기 사례 실패 시 기존 읽기 파일을 보존한다.
        string readJson = JsonConvert.SerializeObject(cases,Formatting.Indented);
        int readCount = cases.Count;
        string writeJson = null;
        if (args.Length > 3) {
            cases.Clear();
            try { AddWriteCases(); }
            catch (Exception error) {
                Console.Error.WriteLine(error.GetType().FullName + ": " + error.Message);
                Environment.Exit(1);
            }
            writeJson = JsonConvert.SerializeObject(cases,Formatting.Indented);
            Console.WriteLine("Write vectors: "+cases.Count);
        }
        File.WriteAllText(args[2],readJson);
        if (writeJson != null)
            File.WriteAllText(args[3],writeJson);
        Console.WriteLine("Golden vectors: "+readCount);
    }
}
public static class JsonCompat { public static JToken GetProperty(this JToken token,string name) => token[name]; public static IEnumerable<JToken> EnumerateArray(this JToken token) => (JArray)token; public static string GetString(this JToken token) => token.Value<string>(); }
