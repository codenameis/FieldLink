using FieldLink.PlcDrivers.Siemens;

namespace FieldLink.Communication.Tests;

internal static partial class ProtocolResponseTests
{
    internal static Task S7InvalidAddressesNeverAliasAnotherLocationAsync()
    {
        foreach (string address in new[] { "M0.8", "M-1", "M0.-1", "M+1", "M1.+1", "M1.2.3",
            "M.1", "M1.", "M 1", "M2147483647", "M536870912", "M2097152", "M2097152.0",
            "DB1", "DB1.DBX0.8", "DB1.0.7.1", "DB-1.0", "DB+1.0", "DB65536.0", "DB1..0",
            "C1.0", "T1.2", "C-1", "T16777216", "", "M", null! })
        {
            TestAssert.True(!S7DeviceAddress.ParseFrom(address, 1).IsSuccess, address);
            TestAssert.True(!SiemensS7NetCommandBuilder.BuildBitReadCommand(address, 1).IsSuccess, address);
            TestAssert.True(!SiemensS7NetCommandBuilder.BuildWriteBitCommand(address, true, 1).IsSuccess, address);
        }
        foreach (var sample in new[] { ("M0.7", 7), ("M1.0", 8), ("DB1.DBX0.7", 7),
            ("D65535.2097151.7", 0xFFFFFF), ("M2097151.7", 0xFFFFFF) })
        {
            var parsed = S7DeviceAddress.ParseFrom(sample.Item1, 1);
            TestAssert.True(parsed.IsSuccess, parsed.Message);
            TestAssert.Equal(sample.Item2, parsed.Content.AddressStart);
            byte[] request = SiemensS7NetCommandBuilder.BuildWriteBitCommand(sample.Item1, true, 1).Content;
            TestAssert.Equal(sample.Item2, request[28] * 65536 + request[29] * 256 + request[30]);
        }
        TestAssert.Equal(7, S7DeviceAddress.ParseFrom("C7", 1).Content.AddressStart);
        TestAssert.Equal(7, S7DeviceAddress.ParseFrom("T7", 1).Content.AddressStart);
        return Task.CompletedTask;
    }

    internal static Task S7MutableAddressesAreValidatedBeforeSerializationAsync()
    {
        foreach (int start in new[] { -1, int.MinValue, 0x1000000, int.MaxValue })
        {
            var address = new S7DeviceAddress { DataCode = 0x83, AddressStart = start, Length = 1 };
            Failed(SiemensS7NetCommandBuilder.BuildReadCommand([address], 1));
            Failed(SiemensS7NetCommandBuilder.BuildWriteByteCommand(address, [1], 1));
            Failed(SiemensS7NetCommandBuilder.BuildWriteByteCommand(
                [S7DeviceAddress.ParseFrom("M0", 1).Content, address], [new byte[] { 1 }, new byte[] { 2 }], 1));
            Failed(SiemensPPICommandBuilder.BuildReadCommand(2, address, 1, false));
        }
        Failed(SiemensS7NetCommandBuilder.BuildReadCommand([null!], 1));
        Failed(SiemensS7NetCommandBuilder.BuildReadCommand(null!, 1));
        Failed(SiemensS7NetCommandBuilder.BuildReadCommand([], 1));
        Failed(SiemensPPICommandBuilder.BuildReadCommand(2, (S7DeviceAddress)null!, 1, false));
        Failed(SiemensPPICommandBuilder.BuildReadCommand(2,
            new S7DeviceAddress { DataCode = 0x84, DbBlock = 256 }, 1, false));
        var mutable = S7DeviceAddress.ParseFrom("M1", 1).Content;
        mutable.AddressStart = 0x1000000;
        Failed(SiemensS7NetCommandBuilder.BuildWriteByteCommand(mutable, [1], 1));
        var last = new S7DeviceAddress { DataCode = 0x83, AddressStart = 0xFFFFFF, Length = 1 };
        TestAssert.True(SiemensS7NetCommandBuilder.BuildReadCommand([last], 1).IsSuccess);
        TestAssert.True(SiemensS7NetCommandBuilder.BuildWriteByteCommand(last, [1], 1).IsSuccess);
        return Task.CompletedTask;
    }

    internal static Task S7WriteLengthsCannotWrapWireFieldsAsync()
    {
        var address = S7DeviceAddress.ParseFrom("M0", 1).Content;
        TestAssert.True(SiemensS7NetCommandBuilder.BuildWriteByteCommand(address, new byte[8191], 1).IsSuccess);
        Failed(SiemensS7NetCommandBuilder.BuildWriteByteCommand(address, new byte[8192], 1));
        Failed(SiemensS7NetCommandBuilder.BuildWriteByteCommand(
            Enumerable.Repeat(address, 256).ToArray(), Enumerable.Range(0, 256).Select(_ => new byte[1]).ToList(), 1));
        var counter = S7DeviceAddress.ParseFrom("C0", 1).Content;
        Failed(SiemensS7NetCommandBuilder.BuildWriteByteCommand(counter, new byte[65536], 1));
        Failed(SiemensS7NetCommandBuilder.BuildWriteByteCommand(
            Enumerable.Repeat(address, 8).ToArray(), Enumerable.Range(0, 8).Select(_ => new byte[8191]).ToList(), 1));
        return Task.CompletedTask;
    }

    internal static Task S7ControlResponsesRejectTruncationAndMalformedEnvelopesAsync()
    {
        foreach (bool start in new[] { true, false })
        {
            // Snap7 서버 PerformFunctionControl: 헤더 뒤 기능 코드 1바이트, 오류·데이터 없음.
            byte[] response = H(start ? "0300001402F08032030000123400010000000028"
                                      : "0300001402F08032030000123400010000000029");
            Func<byte[], FieldLink.PlcDrivers.Common.OperationResult> parse = start
                ? SiemensS7NetResponseParser.CheckStartResult : SiemensS7NetResponseParser.CheckStopResult;
            Failed(parse(null!));
            for (int length = 0; length < response.Length; length++)
                Failed(parse(response.Take(length).ToArray()));
            TestAssert.True(parse(response).IsSuccess);
            foreach (int index in new[] { 0, 1, 3, 4, 5, 6, 7, 8, 14, 16, 19 })
            {
                byte[] bad = (byte[])response.Clone();
                bad[index] ^= 1;
                Failed(parse(bad));
            }
            byte[] error = (byte[])response.Clone();
            error[17] = 0x81;
            error[18] = 4;
            Failed(parse(error), 0x8104);
            Failed(parse(H("0300001302F080320300001234000000008104")), 0x8104);
            // 기존 판정이 성공으로 오인했던 02/07은 정상 응답 매개변수가 아니다.
            byte[] oldStatus = H(start ? "0300001502F0803203000012340002000000002802"
                                       : "0300001502F0803203000012340002000000002907");
            Failed(parse(oldStatus));
            for (int length = 19; length <= 20; length++)
                Failed(parse(oldStatus.Take(length).ToArray()));
            byte[] extra = response.Concat(new byte[] { 0 }).ToArray();
            extra[3]++;
            extra[16] = 1;
            Failed(parse(extra));
        }
        return Task.CompletedTask;
    }
}
