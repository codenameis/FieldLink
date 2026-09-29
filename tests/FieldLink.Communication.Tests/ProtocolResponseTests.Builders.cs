using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Melsec;
using FieldLink.PlcDrivers.Omron;
using FieldLink.PlcDrivers.Siemens;
using FieldLink.PlcDrivers.LSIS;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task McBinaryAndAsciiWordBitWriteCommandsAsync()
    {
        var d = McDeviceAddress.ParseMelsecFrom("D100", 2, false);
        var m = McDeviceAddress.ParseMelsecFrom("M100", 3, false);
        TestAssert.True(d.IsSuccess && m.IsSuccess);
        // 1401/0000 워드 쓰기, 1401/0001 비트 쓰기. 이진 데이터는 하위 바이트 우선이다.
        TestAssert.Bytes(H("01140000640000A802003412CDAB"), McBinaryCommandBuilder.BuildWriteWordCoreCommand(d.Content, H("3412CDAB")));
        TestAssert.Bytes(H("011401006400009003001010"), McBinaryCommandBuilder.BuildWriteBitCoreCommand(m.Content, [true, false, true]));
        TestAssert.Bytes(A("14010000D*00010000021234ABCD"), McAsciiCommandBuilder.BuildAsciiWriteWordCoreCommand(d.Content, H("3412CDAB")));
        TestAssert.Bytes(A("14010001M*0001000003101"), McAsciiCommandBuilder.BuildAsciiWriteBitCoreCommand(m.Content, [true, false, true]));
        Failed(McDeviceAddress.ParseMelsecFrom("UNKNOWN100", 2, false));
        return Task.CompletedTask;
    }

    internal static Task McRUsesFourByteAddressAndTwoByteDeviceCodeAsync()
    {
        var address = new McDeviceAddress { AddressStart = 0x12345678, Length = 2, McDataType = MelsecMcDataType.D };
        // R 계열은 주소 78 56 34 12, 디바이스 코드 A8 00으로 각 필드가 확장된다.
        TestAssert.Bytes(H("0104000078563412A8000200"), MelsecMcRNetCommandBuilder.BuildReadMcCoreCommand(address, false));
        TestAssert.Bytes(H("0114000078563412A80002003412CDAB"), MelsecMcRNetCommandBuilder.BuildWriteWordCoreCommand(address, H("3412CDAB")));
        address.McDataType = MelsecMcDataType.M;
        TestAssert.Bytes(H("0114010078563412900003001010"), MelsecMcRNetCommandBuilder.BuildWriteBitCoreCommand(address, [true, false, true]));
        return Task.CompletedTask;
    }

    internal static Task FinsWriteAndSplitReadAddressesAsync()
    {
        Payload(OmronFinsNetCommandBuilder.BuildWriteWordCommand(OmronPlcType.CSCJ, "D100", H("1234ABCD"), false), "01028200640000021234ABCD");
        Payload(OmronFinsNetCommandBuilder.BuildWriteWordCommand(OmronPlcType.CSCJ, "D100.3", [1, 0, 1], true), "0102020064030003010001");
        var read = OmronFinsNetCommandBuilder.BuildReadCommand(OmronPlcType.CSCJ, "D100", 501, false);
        TestAssert.True(read.IsSuccess, read.Message);
        TestAssert.Equal(2, read.Content.Count);
        TestAssert.Bytes(H("01018200640001F4"), read.Content[0]);
        TestAssert.Bytes(H("0101820258000001"), read.Content[1]); // 500워드 다음 주소는 D600
        var bits = OmronFinsNetCommandBuilder.BuildReadCommand(OmronPlcType.CSCJ, "D100.3", 1999, true);
        TestAssert.True(bits.IsSuccess, bits.Message);
        TestAssert.Equal(2, bits.Content.Count);
        TestAssert.Bytes(H("01010200640307CE"), bits.Content[0]);
        TestAssert.Bytes(H("01010200E1010001"), bits.Content[1]); // D100.3 + 1998비트 = D225.1
        Failed(OmronFinsNetCommandBuilder.BuildWriteWordCommand(OmronPlcType.CSCJ, "UNKNOWN100", [1, 2], false));
        return Task.CompletedTask;
    }

    internal static Task S7WriteAndS7PlusTransportEnvelopeAsync()
    {
        var address = S7DeviceAddress.ParseFrom("DB1.0", 4);
        TestAssert.True(address.IsSuccess, address.Message);
        Payload(SiemensS7NetCommandBuilder.BuildWriteByteCommand(address.Content, H("1234ABCD"), 0x1234),
            "0300002702F080320100001234000E00080501120A10020004000184000000000400201234ABCD");
        TestAssert.Bytes(H("0300000B02F08072010203"), SiemensS7PlusCommandBuilder.BuildWithTPKTAndISO(H("72010203")));
        Failed(S7DeviceAddress.ParseFrom("UNKNOWN1", 2));
        Failed(SiemensS7NetCommandBuilder.BuildWriteBitCommand("UNKNOWN1", true, 1));
        return Task.CompletedTask;
    }

    internal static Task FastEnetWriteByteAddressAndLengthAsync()
    {
        Payload(LSFastEnetCommandBuilder.BuildWriteByteCommand(new FastEnetFrameOptions(), "D100", H("3412CDAB")),
            "5800140000000100060025444232303004003412CDAB");
        Failed(LSFastEnetCommandBuilder.BuildWriteByteCommand(new FastEnetFrameOptions(), "?100", [1, 2]));
        return Task.CompletedTask;
    }
}
