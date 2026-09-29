using System.Text;
using FieldLink.Robot.ABB.Protocols;
using FieldLink.Robot.Estun.Protocols;
using FieldLink.Robot.FANUC.Protocols;
using FieldLink.Robot.Hyundai.Protocols;
using FieldLink.Robot.YASKAWA.Protocols;

namespace FieldLink.Communication.Tests;

internal static partial class RobotProtocolTests
{
    private static byte[] YrcResponse(params byte[] payload)
    {
        byte[] response = new byte[32 + payload.Length];
        Encoding.ASCII.GetBytes("YERC").CopyTo(response, 0); response[4] = 32;
        response[6] = (byte)payload.Length; response[7] = (byte)(payload.Length >> 8);
        payload.CopyTo(response, 32); return response;
    }

    internal static Task RobotYrcEthernetPayloadsAsync()
    {
        var builder = new YrcEthernetRequestBuilder();
        byte[] stats = YrcResponse(1, 0, 0, 0, 2, 0, 0, 0);
        var flags = builder.BuildReadStats().ParseResponse(stats).Content;
        TestAssert.Equal(16, flags.Length); TestAssert.True(flags[0] && flags[9] && !flags[8]);
        TestAssert.Equal((byte)0x81, builder.BuildReadIO(1).ParseResponse(YrcResponse(0x81)).Content);
        TestAssert.Bytes([0x81, 0x42], builder.BuildReadIO(1, 2).ParseResponse(YrcResponse(2, 0, 0, 0, 0x81, 0x42)).Content);
        TestAssert.Equal((ushort)0x1234, builder.BuildReadRegisterVariable(1).ParseResponse(YrcResponse(0x34, 0x12)).Content);
        TestAssert.Equal((short)-7, builder.BuildReadIntegerVariable(1).ParseResponse(YrcResponse(0xf9, 0xff)).Content);
        TestAssert.Equal(123456, builder.BuildReadDoubleIntegerVariable(1).ParseResponse(YrcResponse(0x40, 0xe2, 1, 0)).Content);
        TestAssert.Equal(1.5f, builder.BuildReadRealVariable(1).ParseResponse(YrcResponse(0, 0, 0xc0, 0x3f)).Content);
        // 여러 변수 응답은 원본처럼 개수 접두어를 제거하지 않는다.
        TestAssert.Equal((ushort)2, builder.BuildReadRegisterVariable(1, 2).ParseResponse(YrcResponse(2, 0, 0x34, 0x12)).Content[0]);
        TestAssert.Bytes([2, 0, 0, 0, 7, 8], builder.BuildReadByteVariable(1, 2).ParseResponse(YrcResponse(2, 0, 0, 0, 7, 8)).Content);
        TestAssert.Equal("AB\0", builder.BuildReadStringVariable(1).ParseResponse(YrcResponse(65, 66, 0)).Content);
        byte[] strings = new byte[32]; Encoding.ASCII.GetBytes("FIRST").CopyTo(strings, 0); Encoding.ASCII.GetBytes("SECOND").CopyTo(strings, 16);
        var text = builder.BuildReadStringVariable(1, 2).ParseResponse(YrcResponse(strings)).Content;
        TestAssert.Equal("FIRST" + new string('\0', 11), text[0]); TestAssert.Equal("SECOND" + new string('\0', 10), text[1]);
        return Task.CompletedTask;
    }

    internal static Task RobotYrcTypedErrorPropagationAsync()
    {
        var builder = new YrcEthernetRequestBuilder();
        byte[] error = YrcResponse(); error[25] = 0x1f; error[26] = 2; error[28] = 0x10; error[29] = 0x20;
        var requests = new object[] { builder.BuildReadStats(), builder.BuildReadJSeq(), builder.BuildReadPose(), builder.BuildReadTorqueData(), builder.BuildReadIO(1), builder.BuildReadIO(1, 2), builder.BuildReadRegisterVariable(1), builder.BuildReadByteVariable(1), builder.BuildReadIntegerVariable(1), builder.BuildReadDoubleIntegerVariable(1), builder.BuildReadRealVariable(1), builder.BuildReadStringVariable(1), builder.BuildReadStringVariable(1, 2), builder.BuildReadManagementTime(1), builder.BuildReadManagementTimeSpan(1), builder.BuildReadSystemInfo(11), builder.BuildReset(), builder.BuildReadAlarms()[0] };
        foreach (object request in requests)
        {
            var result = (FieldLink.PlcDrivers.Common.OperationResult)request.GetType().GetMethod("ParseResponse")!.Invoke(request, [error])!;
            TestAssert.True(!result.IsSuccess); TestAssert.Equal(31, result.ErrorCode); TestAssert.Equal("로봇 동작 중", result.Message);
        }
        // 원본 ByteTransformHelper가 감싸던 변환은 짧은 본문에서도 실패 결과를 반환한다.
        TestAssert.True(!builder.BuildReadByteVariable(1).ParseResponse(YrcResponse()).IsSuccess);
        TestAssert.True(!builder.BuildReadRealVariable(1).ParseResponse(YrcResponse(0)).IsSuccess);
        error[26] = 1; error[28] = 8;
        TestAssert.True(!builder.BuildReset().ParseResponse(error).IsSuccess);
        return Task.CompletedTask;
    }

    internal static Task RobotYrcStructuredPayloadsAsync()
    {
        var builder = new YrcEthernetRequestBuilder();
        byte[] job = new byte[44]; Encoding.ASCII.GetBytes("JOB").CopyTo(job, 0); job[32] = 12; job[36] = 3; job[40] = 80;
        var seq = builder.BuildReadJSeq().ParseResponse(YrcResponse(job)).Content;
        TestAssert.Equal("JOB" + new string('\0', 29), seq[0]); TestAssert.Equal("12", seq[1]); TestAssert.Equal("3", seq[2]); TestAssert.Equal("80", seq[3]);
        byte[] pose = new byte[28]; pose[20] = 10; pose[24] = 20;
        TestAssert.True(builder.BuildReadPose().ParseResponse(YrcResponse(pose)).Content.SequenceEqual(new[] { "10", "20" }));
        TestAssert.True(builder.BuildReadTorqueData().ParseResponse(YrcResponse(10, 0, 0, 0, 20, 0, 0, 0)).Content.SequenceEqual(new[] { "10", "20" }));
        byte[] info = new byte[48]; Encoding.ASCII.GetBytes("VER").CopyTo(info, 0); Encoding.ASCII.GetBytes("MODEL").CopyTo(info, 24); Encoding.ASCII.GetBytes("PARAM").CopyTo(info, 40);
        var parts = builder.BuildReadSystemInfo(11).ParseResponse(YrcResponse(info)).Content;
        TestAssert.Equal(24, parts[0].Length); TestAssert.True(parts[1].StartsWith("MODEL")); TestAssert.Equal("PARAM\0\0\0", parts[2]);
        var time = builder.BuildReadManagementTime(1).ParseResponse(YrcResponse(Encoding.ASCII.GetBytes("2026/09/27 12:34"))).Content;
        TestAssert.Equal(new DateTime(2026, 9, 27, 12, 34, 0), time);
        TestAssert.Equal("000000000123", builder.BuildReadManagementTimeSpan(1).ParseResponse(YrcResponse(Encoding.ASCII.GetBytes("000000000123"))).Content);
        byte[] alarm = new byte[36]; alarm[0] = 7; Encoding.ASCII.GetBytes("2026/09/27 12:34").CopyTo(alarm, 16); Encoding.ASCII.GetBytes("ALRM").CopyTo(alarm, 32);
        var item = builder.BuildReadAlarms()[0].ParseResponse(YrcResponse(alarm)).Content;
        TestAssert.Equal(7, item.AlarmCode); TestAssert.Equal("ALRM", item.Message);
        TestAssert.True(builder.BuildReadAlarms()[0].ParseResponse(YrcResponse()).Content == null);
        return Task.CompletedTask;
    }

    internal static Task RobotYrcRequestIdentityAndAlarmsAsync()
    {
        var builder = new YrcEthernetRequestBuilder(); var request = builder.BuildReset();
        byte[] one = request.Build(1); byte[] two = request.Build(2); one[0] = 0;
        TestAssert.Equal((byte)'Y', two[0]); TestAssert.Equal((byte)2, two[11]); TestAssert.Equal((byte)255, request.Build(255)[11]);
        var alarms = builder.BuildReadAlarms(); TestAssert.Equal(4, alarms.Length);
        for (int i = 0; i < 4; i++) TestAssert.Equal((ushort)(i + 1), BitConverter.ToUInt16(alarms[i].Build((byte)i), 26));
        var history = builder.BuildReadHistoryAlarms(1001, 3);
        foreach (var entry in history) TestAssert.Equal((ushort)1001, BitConverter.ToUInt16(entry.Build(1), 26));
        return Task.CompletedTask;
    }

    internal static Task RobotYrcTcpHandshakeAsync()
    {
        TestAssert.Bytes(Encoding.ASCII.GetBytes("CONNECT Robot_access KeepAlive:-1\r\n"), YrcTcpProtocol.BuildConnect());
        TestAssert.True(YrcTcpProtocol.ParseConnect("OK:YR Information Server(Ver) Keep-Alive:-1.\r\n").Content);
        TestAssert.True(!YrcTcpProtocol.ParseConnect("OK:other\r\n").Content);
        TestAssert.True(!YrcTcpProtocol.ParseConnect("ERROR\r\n").IsSuccess);
        var request = YrcTcpRequestBuilder.BuildJSeq("JOB", 12);
        TestAssert.Bytes(Encoding.ASCII.GetBytes("HOSTCTRL_REQUEST JSEQ 7\r\n"), request.BuildHeader());
        TestAssert.Bytes(Encoding.ASCII.GetBytes("JOB,12\r"), request.BuildBody());
        TestAssert.Bytes([], YrcTcpRequestBuilder.BuildReset().BuildBody());
        TestAssert.True(YrcTcpProtocol.CheckHeaderResponse("OK: JSEQ\r\n").IsSuccess);
        TestAssert.Equal("ERROR", YrcTcpProtocol.CheckHeaderResponse("ERROR\r\n").Message);
        TestAssert.True(YrcTcpProtocol.RequiresTrailingLf("0000\r")); TestAssert.True(YrcTcpProtocol.RequiresTrailingLf("ERROR: (2010).\r"));
        TestAssert.True(!YrcTcpProtocol.RequiresTrailingLf("123\r"));
        TestAssert.Equal("0000", request.ParseResponse("0000\r").Content);
        TestAssert.Equal("ABC", YrcTcpProtocol.ParseResponse("ABC\r").Content);
        TestAssert.True(!YrcTcpProtocol.ParseResponse("").IsSuccess);
        TestAssert.True(YrcErrorParser.ExtraErrorMessage("ERROR: (2010).").Message.Contains("로봇 동작 중"));
        TestAssert.True(!YrcErrorParser.ExtraErrorMessage("ERROR: (2010).\r").Message.Contains("로봇 동작 중"));
        return Task.CompletedTask;
    }

    internal static Task RobotYrcTcpMappingsAsync()
    {
        var commands = new (YrcTcpRequest<string> Request, string Command, string? Data)[] {
            (YrcTcpRequestBuilder.BuildReadAlarm(), "RALARM", null), (YrcTcpRequestBuilder.BuildReadPosJ(), "RPOSJ", null),
            (YrcTcpRequestBuilder.BuildReadJSeq(), "RJSEQ", null), (YrcTcpRequestBuilder.BuildReadUFrame(3), "RUFRAME", "3"),
            (YrcTcpRequestBuilder.BuildReadByteVariable("10"), "SAVEV", "0,10"), (YrcTcpRequestBuilder.BuildReadIntegerVariable("10"), "SAVEV", "1,10"),
            (YrcTcpRequestBuilder.BuildReadDoubleIntegerVariable("10"), "SAVEV", "2,10"), (YrcTcpRequestBuilder.BuildReadRealVariable("10"), "SAVEV", "3,10"),
            (YrcTcpRequestBuilder.BuildReadStringVariable("10"), "SAVEV", "7,10"), (YrcTcpRequestBuilder.BuildHold(true), "HOLD", "1"),
            (YrcTcpRequestBuilder.BuildReset(), "RESET", null), (YrcTcpRequestBuilder.BuildCancel(), "CANCEL", null),
            (YrcTcpRequestBuilder.BuildMode(2), "MODE", "2"), (YrcTcpRequestBuilder.BuildCycle(3), "CYCLE", "3"),
            (YrcTcpRequestBuilder.BuildSvon(false), "SVON", "0"), (YrcTcpRequestBuilder.BuildHLock(true), "HLOCK", "1"),
            (YrcTcpRequestBuilder.BuildDisplayMessage("MSG"), "MDSP", "MSG"), (YrcTcpRequestBuilder.BuildStart("JOB"), "START", "JOB"),
            (YrcTcpRequestBuilder.BuildDelete("JOB"), "DELETE", "JOB"), (YrcTcpRequestBuilder.BuildSetMainJob("JOB"), "SETMJ", "JOB"),
            (YrcTcpRequestBuilder.BuildReadString("SAVEV;7,10;ignored"), "SAVEV", "7,10"), (YrcTcpRequestBuilder.BuildReadString("SAVEV;;ignored"), "SAVEV", "")
        };
        foreach (var item in commands)
        {
            int length = item.Data == null || item.Data.Length == 0 ? 0 : item.Data.Length + 1;
            TestAssert.Bytes(Encoding.ASCII.GetBytes($"HOSTCTRL_REQUEST {item.Command} {length}\r\n"), item.Request.BuildHeader());
            TestAssert.Bytes(Encoding.ASCII.GetBytes(length == 0 ? "" : item.Data + "\r"), item.Request.BuildBody());
        }
        TestAssert.True(!YrcTcpRequestBuilder.BuildIOWrite(27010, [true]).IsSuccess);
        var io = YrcTcpRequestBuilder.BuildIOWrite(27010, [true, false, true, false, false, false, false, true]).Content;
        TestAssert.Bytes(Encoding.ASCII.GetBytes("27010,8,133\r"), io.BuildBody());
        TestAssert.True(YrcTcpRequestBuilder.BuildIORead(1, 8).ParseResponse("129\r").Content[7]);
        var pose = YrcTcpRequestBuilder.BuildReadPosC(1, false).ParseResponse("1,2,3,4,5,6,5,7\r").Content;
        TestAssert.Equal(6f, pose.Rz); TestAssert.Equal(7, pose.ToolNumber);
        TestAssert.Bytes(Encoding.ASCII.GetBytes("1,0\r"), YrcTcpRequestBuilder.BuildReadPosC(1, false).BuildBody());
        return Task.CompletedTask;
    }

    internal static Task RobotAbbRequestsAsync()
    {
        TestAssert.Equal("/rw/panel/ctrlstate", AbbRequestBuilder.GetCtrlState().Path);
        TestAssert.Equal("motoron", AbbRequestBuilder.GetCtrlState().ParseResponse("<span class=\"ctrlstate\">motoron</span>").Content);
        TestAssert.True(!AbbRequestBuilder.GetCtrlState().ParseResponse("<body/>").IsSuccess);
        TestAssert.Equal("/rw/motionsystem/mechunits/ROB_2/jointtarget", AbbRequestBuilder.GetJointTarget("ROB_2").Path);
        TestAssert.Equal("/rw/rapid/symbol/data/RAPID/T_ROB1/user/count", AbbRequestBuilder.GetUserValue("count").Path);
        TestAssert.Equal("/custom", AbbRequestBuilder.GetUserValue("url=/custom").Path);
        TestAssert.Equal("/rw/iosystem/signals/Local/U/S", AbbRequestBuilder.GetAnIOSignal("Local", "U", "S").Path);
        TestAssert.Equal("/rw/elog/0?lang=zh&resource=title", AbbRequestBuilder.GetLog().Path);
        foreach (string alias in AbbRequestBuilder.GetSelectStrings()) TestAssert.True(AbbRequestBuilder.BuildByAddress(alias).IsSuccess);
        TestAssert.Equal(AbbRequestBuilder.GetJointTarget().Path, AbbRequestBuilder.BuildByAddress("PhysicalJoints").Content.Path);
        TestAssert.True(!AbbRequestBuilder.BuildByAddress("unknown").IsSuccess);
        TestAssert.Equal("/custom", AbbRequestBuilder.BuildByAddress("url=/custom").Content.Path);
        TestAssert.Equal("[]", AbbRequestBuilder.GetLog(0).ParseResponse("<li class=\"elog-message-li\"><span class=\"name\">A</span></li>").Content);
        return Task.CompletedTask;
    }

    internal static Task RobotEstunSequenceAsync()
    {
        var plan = EstunProtocol.BuildCommand(EstunProtocol.StartProgram);
        TestAssert.Equal(9, plan.Length);
        TestAssert.Equal((ushort)99, plan[0].Address); TestAssert.Equal((ushort)51, plan[1].Address); TestAssert.Equal(EstunStepKind.CheckIdle, plan[2].Kind);
        TestAssert.Bytes([0, 0x11], plan[3].Data);
        TestAssert.Equal((ushort)18, plan[4].Address); TestAssert.Equal((short)0x801, plan[4].ExpectedValue!.Value); TestAssert.Equal(20, plan[4].MaxAttempts); TestAssert.Equal(100, plan[4].DelayMilliseconds);
        TestAssert.Bytes([0, 4], plan[5].Data); TestAssert.Equal(100, plan[6].DelayMilliseconds); TestAssert.True(plan[6].ExpectedValue == null);
        TestAssert.Bytes([0, 0], plan[7].Data); TestAssert.Bytes([0, 0], plan[8].Data);
        TestAssert.True(EstunProtocol.CheckIdle(0, 0).IsSuccess); TestAssert.True(!EstunProtocol.CheckIdle(1, 0).IsSuccess); TestAssert.True(!EstunProtocol.CheckIdle(0, 1).IsSuccess);
        var load = EstunProtocol.BuildLoadProject("JOB"); TestAssert.Equal((ushort)53, load[0].Address); TestAssert.Equal(20, load[0].Data.Length); TestAssert.Bytes([0, 0x80], load[6].Data);
        var speed = EstunProtocol.BuildSetGlobalSpeed(50); TestAssert.Equal((ushort)52, speed[0].Address); TestAssert.Bytes([0, 50], speed[0].Data); TestAssert.Bytes([2, 0], speed[6].Data);
        return Task.CompletedTask;
    }

    internal static Task RobotFanucInitializationAndResponsesAsync()
    {
        var init = FanucProtocol.BuildInitialization(); TestAssert.Equal(62, init.Length);
        TestAssert.Bytes([0, 4, 0, 0], init[0].Skip(1).Take(4).ToArray()); TestAssert.Equal((byte)8, init[1][0]); TestAssert.Equal((byte)1, init[1][2]);
        TestAssert.Bytes(Encoding.ASCII.GetBytes("CLRASG"), init[2].Skip(48).Take(6).ToArray());
        init[0][1] = 1; TestAssert.Equal((byte)0, FanucProtocol.BuildInitialization()[0][1]);
        byte[] inline = new byte[56]; inline[31] = 0xd4; inline[44] = 0x34; inline[45] = 0x12;
        TestAssert.Bytes([0x34, 0x12], FanucProtocol.ParseReadResponse(inline, 1).Content);
        byte[] extended = new byte[58]; extended[31] = 0x94; extended[56] = 0x78; extended[57] = 0x56;
        TestAssert.Bytes([0x78, 0x56], FanucProtocol.ParseReadResponse(extended, 1).Content);
        inline[31] = 0xab; TestAssert.Equal(0xab, FanucProtocol.ParseReadResponse(inline, 1).ErrorCode);
        // 원본 기본 실패 결과의 오류 코드는 10000이다.
        TestAssert.Equal(0xab, FanucProtocol.ParseWriteResponse(inline).ErrorCode); TestAssert.Equal(10000, FanucProtocol.ParseWriteResponse(inline, true).ErrorCode);
        TestAssert.True(!FanucProtocol.ParseWriteResponse(inline, true).IsSuccess);
        inline[31] = 0xd4; inline[44] = 0x81; inline[45] = 1;
        var bits = FanucProtocol.ParseBitResponse(inline, 8, 2).Content; TestAssert.True(bits.SequenceEqual(new[] { true, true }));
        TestAssert.True(!FanucProtocol.BuildRead("I1", 1).IsSuccess); TestAssert.True(!FanucProtocol.BuildWrite("D1", new[] { true }).IsSuccess);
        var bitCommand = FanucProtocol.BuildWriteBits(76, 8, [true, true]); TestAssert.Bytes([0x80, 1], bitCommand.Skip(48).Take(2).ToArray());
        var joint = FanucProtocol.BuildWriteJoint(100, new float[9], -1, 3); TestAssert.Equal(3, joint.Length);
        // R-007: 이전 +44의 [0, UT] 대신 +46의 UT 한 워드만 기록한다.
        TestAssert.Equal((ushort)145, BitConverter.ToUInt16(joint[2], 44)); TestAssert.Bytes([3, 0], joint[2].Skip(48).Take(2).ToArray());
        return Task.CompletedTask;
    }

    internal static Task RobotFanucServerPacketsAsync()
    {
        // 요청은 고정 헤더 필드를 직접 배치하여 빌더와 독립적으로 만든다.
        byte[] word = new byte[56]; word[2] = 8; word[43] = 8; word[44] = 99; word[46] = 2; word[48] = 1; word[50] = 2;
        var parsed = FanucServerProtocol.ParseRequest(word).Content;
        TestAssert.Equal((ushort)99, parsed.Address); TestAssert.True(parsed.IsWord); TestAssert.Bytes([1, 0, 2, 0], parsed.Data);
        word[43] = 76; word[44] = 7; word[48] = 0x80; word[49] = 1;
        parsed = FanucServerProtocol.ParseRequest(word).Content; TestAssert.True(!parsed.IsWord); TestAssert.Bytes([1, 1], parsed.Data);
        word[2] = 99; TestAssert.True(!FanucServerProtocol.ParseRequest(word).IsSuccess);
        var reply = FanucServerProtocol.BuildWriteReply(8); TestAssert.Equal((byte)8, reply[2]); TestAssert.Equal((byte)9, reply[30]); TestAssert.Equal((byte)0xd4, reply[31]);
        TestAssert.Equal((byte)8, FanucServerProtocol.BuildWriteReply(8, true)[30]);
        TestAssert.Equal(56, FanucServerProtocol.BuildConnectReply().Length); TestAssert.Equal((byte)1, FanucServerProtocol.BuildConnectReply()[8]);
        TestAssert.Equal((byte)0xd4, FanucServerProtocol.BuildSessionReply()[31]);
        return Task.CompletedTask;
    }

    internal static async Task RobotPreservedMalformedInputBehaviorAsync()
    {
        byte[] response = new byte[56]; response[31] = 0x94;
        await TestAssert.ThrowsAsync<ArgumentException>(() => Task.FromResult(FanucProtocol.ParseBitResponse(response, 1, 1)));
        await TestAssert.ThrowsAsync<IndexOutOfRangeException>(() => Task.FromResult(FanucProtocol.ParseReadResponse(new byte[31], 1)));
        // Value conversion now rejects a truncated range before reading the buffer.
        await TestAssert.ThrowsAsync<ArgumentOutOfRangeException>(() => Task.FromResult(EstunProtocol.ParseData(new byte[199])));
        await TestAssert.ThrowsAsync<IndexOutOfRangeException>(() => Task.FromResult(new YrcEthernetRequestBuilder().BuildReadIO(1).ParseResponse(YrcResponse())));
    }
}
