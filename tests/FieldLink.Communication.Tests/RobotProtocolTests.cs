using System.Text;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.EFORT.Protocols;
using FieldLink.Robot.Estun.Protocols;
using FieldLink.Robot.FANUC.Protocols;
using FieldLink.Robot.Hyundai.Protocols;
using FieldLink.Robot.KUKA.Protocols;
using FieldLink.Robot.YAMAHA.Protocols;

namespace FieldLink.Communication.Tests;

internal static partial class RobotProtocolTests
{
    internal static Task RobotEfortPacketsAsync()
    {
        // 원본 GetReadCommand의 고정 표식, 길이, 명령 1001과 하트비트 위치.
        TestAssert.Bytes(TestBytes.FromHexString("4D6573736167654865616400000000002600E90334124D6573736167655461696C0000000000"), EfortProtocol.BuildReadCommand(0x1234));
        TestAssert.Bytes(TestBytes.FromHexString("4D65737361676548656164000000002400E90334124D6573736167655461696C00000000"), EfortProtocol.BuildPreviousReadCommand(0x1234));
        TestAssert.True(!EfortControllerState.ParseFrom(new byte[787]).IsSuccess);
        TestAssert.True(!EfortControllerState.ParseFromPrevious(new byte[783]).IsSuccess);
        byte[] current = new byte[788]; current[22] = 1; current[30] = 3; current[768] = 42;
        var parsed = EfortControllerState.ParseFrom(current).Content;
        TestAssert.Equal((byte)1, parsed.ErrorStatus);
        TestAssert.Equal((ushort)3, parsed.ModeStatus);
        TestAssert.Equal(42, parsed.DbDeviceTime);
        byte[] previous = new byte[784]; previous[21] = 1; previous[29] = 2; previous[765] = 13;
        parsed = EfortControllerState.ParseFromPrevious(previous).Content;
        TestAssert.Equal((ushort)2, parsed.ModeStatus);
        TestAssert.Equal(13, parsed.DbDeviceTime);
        return Task.CompletedTask;
    }

    internal static Task RobotFanucFramesAsync()
    {
        TestAssert.Bytes(TestBytes.FromHexString("02000600000000000001000000000000000100000000000000000000000006C000000000100E000001010408630032000000000000000000"), FanucProtocol.BuildReadData(8, 100, 50));
        var address = FanucProtocol.ParseAddress("SDO11001", true);
        TestAssert.True(address.IsSuccess);
        TestAssert.Equal((byte)76, address.Content1);
        TestAssert.Equal((ushort)1, address.Content2);
        TestAssert.True(!FanucProtocol.ParseAddress("SR7", false).IsSuccess);
        TestAssert.Equal(60, FanucProtocol.GetAssignmentCommands().Length);
        TestAssert.Equal("CLRASG", FanucProtocol.GetAssignmentCommands()[0]);
        return Task.CompletedTask;
    }

    internal static Task RobotHyundaiUnitsAsync()
    {
        // 원본 LoadBy의 단위 변환: wire 1m -> 1000mm, pi rad -> 180도.
        byte[] packet = new byte[64]; packet[0] = (byte)'P'; packet[4] = 2; packet[8] = 7;
        TestBytes.FromHexString("000000000000F03F").CopyTo(packet, 16);
        TestBytes.FromHexString("182D4454FB210940").CopyTo(packet, 40);
        var result = HyundaiProtocol.Parse(packet);
        TestAssert.True(result.IsSuccess);
        TestAssert.Equal(1000d, result.Content.Data[0]);
        TestAssert.Equal(180d, result.Content.Data[3]);
        TestAssert.Bytes(packet, HyundaiProtocol.BuildIncrement(7, 1000, 0, 0, 180, 0, 0));
        TestAssert.True(!HyundaiProtocol.Parse(new byte[63]).IsSuccess);
        return Task.CompletedTask;
    }

    internal static Task RobotTextAndProxyAsync()
    {
        TestAssert.Equal("00A,B", KukaTextProtocol.BuildReadCommands(["A", "B"]));
        TestAssert.Equal("01A=1,B=2", KukaTextProtocol.BuildWriteCommands(["A", "B"], ["1", "2"]));
        TestAssert.True(!KukaTextProtocol.ParseCommandResponse(Encoding.UTF8.GetBytes("err: failure")).IsSuccess);
        // 요청 길이/ID는 원본 ProtocolValueConverter(CDAB)의 ushort 빅 엔디언이며 응답도 같다.
        TestAssert.Bytes([0x12, 0x34, 0, 4, 0, 0, 1, (byte)'A'], KukaVarProxyProtocol.PackCommand(KukaVarProxyProtocol.BuildReadValueCommand("A"), 0x1234));
        TestAssert.Bytes([(byte)'X'], KukaVarProxyProtocol.ExtractActualData([0, 1, 0, 7, 0, 0, 1, (byte)'X', 0, 1, 1]).Content);
        TestAssert.True(!KukaVarProxyProtocol.ExtractActualData([0]).IsSuccess);
        TestAssert.Bytes(Encoding.ASCII.GetBytes("@ RESET \r\n"), YamahaRcxProtocol.BuildReset());
        // RCX340-PRO_E_V1.20 section 2.1 uses ASCII @, not replacement '?'.
        TestAssert.Bytes(Encoding.ASCII.GetBytes("@ LOAD <MAIN>, T1\r\n"), YamahaRcxProtocol.BuildLoad("MAIN", 1));
        TestAssert.Equal(2, YamahaRcxProtocol.ParseStatus(["2", "OK"]).Content);
        TestAssert.True(!YamahaRcxProtocol.ParseStatus(["2", "NG"]).IsSuccess);
        TestAssert.Bytes(Encoding.ASCII.GetBytes("@ JOGXY [2] 3-\r\n"), YamahaRcxProtocol.BuildJogXY(-3, 2));
        var sequence = YamahaRcxProtocol.BuildExchangeSequence(YamahaRcxProtocol.BuildReadMotorStatus(), 2);
        TestAssert.Equal(2, sequence.Length);
        TestAssert.Bytes(Encoding.ASCII.GetBytes("@?MOTOR \r\n"), sequence[0]);
        TestAssert.Bytes([], sequence[1]);
        sequence[0][0] = 0;
        TestAssert.Equal((byte)'@', YamahaRcxProtocol.BuildReadMotorStatus()[0]);
        TestAssert.Equal("OK", YamahaRcxProtocol.ParseLine(Encoding.ASCII.GetBytes("OK\r\n")));
        return Task.CompletedTask;
    }

    internal static Task RobotFrameLengthsAsync()
    {
        TestAssert.Equal(0x1234, new FanucFrameRules().GetBodyLength(new byte[] { 0, 0, 0, 0, 0x34, 0x12 }.Concat(new byte[50]).ToArray()));
        byte[] head = new byte[18]; head[16] = 38;
        TestAssert.Equal(20, new EfortFrameRules().GetBodyLength(head));
        head[16] = 0;
        TestAssert.Equal(0, new EfortFrameRules().GetBodyLength(head));
        byte[] proxyHeader = [0x12, 0x34, 0, 8];
        var proxy = new KukaVarProxyFrameRules();
        TestAssert.Equal(8, proxy.GetBodyLength(proxyHeader)); TestAssert.Equal<int?>(0x1234, proxy.GetSequenceId(proxyHeader));
        return Task.CompletedTask;
    }
}
