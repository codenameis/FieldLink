using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using FieldLink.PacketBench.Connections;
using FieldLink.PacketBench.Diagnostics;
using FieldLink.PacketBench.Operations;
using FieldLink.PacketBench.VirtualPlc;
using FieldLink.Plc;
using FieldLink.Plc.Protocols.Modbus;
using FieldLink.Transport;

namespace FieldLink.PacketBench;

/// <summary>UI 없이 가상 PLC 와 클라이언트의 왕복을 확인합니다. 결과는 selftest-result.txt 에 남습니다.</summary>
internal static class SelfTest
{
    public static int Run()
    {
        var lines = new List<string>();
        int failures = 0;

        void Check(string name, bool condition, string detail)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? " — " + detail : string.Empty)}");
            if (!condition)
            {
                failures++;
            }
        }

        try
        {
            RunModbusChecks(Check);
            RunMcChecks(Check);
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL  예외 — {exception.GetType().Name}: {exception.Message}");
            failures++;
        }

        return Finish(lines, failures);
    }

    private static void RunModbusChecks(Action<string, bool, string> check)
    {
        var log = new PacketLog { RecordServerFrames = true };
        var memory = new VirtualPlcMemory(
            coilCount: 4096,
            discreteInputCount: 4096,
            holdingRegisterCount: 4096,
            inputRegisterCount: 4096);
        memory.FillTestPattern();

        using var server = new VirtualPlcServer(log);
        int port = StartServer(server, new ModbusTcpResponder(memory), 15020);
        check("Modbus 가상 PLC 시작", server.IsRunning, $"127.0.0.1:{port}");

        var settings = new ConnectionSettings { Host = "127.0.0.1", Port = port, TimeoutMilliseconds = 1000 };
        ClientDescriptor descriptor = FindClient("modbus-tcp");
        ITransport transport = new RecordingTransport(settings.CreateTransport(TransportKind.Tcp), log);
        using PlcClient client = descriptor.CreateClient(transport, settings);

        PlcResult openResult = client.OpenAsync().GetAwaiter().GetResult();
        check("Modbus 연결", openResult.IsSuccess, openResult.IsSuccess ? descriptor.DisplayName : openResult.Error.Message);
        if (!openResult.IsSuccess)
        {
            return;
        }

        string[] words = { "100", "200", "0x00FF", "65535", "7" };
        WriteOutcome blockWrite = WriteExecutor.ExecuteAsync(
            client, "40001", WriteInterpretation.Word, WriteMode.Block, words, verify: true, CancellationToken.None)
            .GetAwaiter().GetResult();
        check("블록 연속 쓰기", blockWrite.IsSuccess, blockWrite.Message);
        check("쓰기 검증", blockWrite.IsVerified == true, blockWrite.VerificationMessage);

        PlcResult<ReadOutcome> wordRead = ReadExecutor.ExecuteAsync(
            client, "40001", ReadInterpretation.Word, CountUnit.Word, words.Length, client.ByteOrder, CancellationToken.None)
            .GetAwaiter().GetResult();
        check("연속 읽기", wordRead.IsSuccess, wordRead.IsSuccess ? wordRead.Value.Summary : wordRead.Error.Message);

        if (wordRead.IsSuccess)
        {
            string joined = string.Join(",", wordRead.Value.Rows.Select(row => row.Value));
            check("읽은 값 일치", joined == "100,200,255,65535,7", joined);
        }

        WriteOutcome coilWrite = WriteExecutor.ExecuteAsync(
            client, "00001", WriteInterpretation.Bit, WriteMode.PerElement, new[] { "1", "0", "1", "1" }, verify: true, CancellationToken.None)
            .GetAwaiter().GetResult();
        check("코일 주소 증가 쓰기", coilWrite.IsSuccess && coilWrite.IsVerified == true, coilWrite.VerificationMessage);

        PlcResult<ReadOutcome> bitRead = ReadExecutor.ExecuteAsync(
            client, "00001", ReadInterpretation.Bit, CountUnit.Bit, 4, client.ByteOrder, CancellationToken.None)
            .GetAwaiter().GetResult();
        string bitText = bitRead.IsSuccess ? string.Join(",", bitRead.Value.Rows.Select(row => row.Value)) : bitRead.Error.Message;
        check("코일 읽기", bitRead.IsSuccess && bitText == "1,0,1,1", bitText);

        check(
            "가상 PLC 메모리 반영",
            memory.GetRegister(ModbusArea.HoldingRegister, 4) == 7,
            $"40005 = {memory.GetRegister(ModbusArea.HoldingRegister, 4)}");

        server.Settings.ForcedExceptionCode = (byte)ModbusExceptionCode.IllegalDataAddress;
        PlcResult<ReadOutcome> forcedRead = ReadExecutor.ExecuteAsync(
            client, "40001", ReadInterpretation.Word, CountUnit.Word, 1, client.ByteOrder, CancellationToken.None)
            .GetAwaiter().GetResult();
        check(
            "강제 예외 응답",
            !forcedRead.IsSuccess
                && forcedRead.Error.Kind == PlcErrorKind.DeviceError
                && forcedRead.Error.DeviceErrorCode == (int)ModbusExceptionCode.IllegalDataAddress,
            forcedRead.IsSuccess ? "예상과 달리 성공" : forcedRead.Error.ToString());
        server.Settings.ForcedExceptionCode = 0;

        server.Settings.SuppressResponse = true;
        var stopwatch = Stopwatch.StartNew();
        PlcResult<ReadOutcome> timeoutRead = ReadExecutor.ExecuteAsync(
            client, "40001", ReadInterpretation.Word, CountUnit.Word, 1, client.ByteOrder, CancellationToken.None)
            .GetAwaiter().GetResult();
        stopwatch.Stop();
        check(
            "응답 없음 → 제한 시간",
            !timeoutRead.IsSuccess && timeoutRead.Error.Kind == PlcErrorKind.Timeout,
            $"{stopwatch.ElapsedMilliseconds} ms");
        server.Settings.SuppressResponse = false;

        check("Modbus 패킷 기록", log.SentCount > 0 && log.ReceivedCount > 0, $"TX {log.SentCount} / RX {log.ReceivedCount}");

        client.CloseAsync().GetAwaiter().GetResult();
        server.Stop();
        check("Modbus 정지", !server.IsRunning, string.Empty);
    }

    private static void RunMcChecks(Action<string, bool, string> check)
    {
        var log = new PacketLog { RecordServerFrames = true };
        var memory = new Mc3EMemory();
        memory.Clear();

        using var server = new VirtualPlcServer(log);
        int port = StartServer(server, new Mc3EBinaryResponder(memory), 15040);
        check("MC 가상 PLC 시작", server.IsRunning, $"127.0.0.1:{port} (MC 3E Binary)");

        var settings = new ConnectionSettings { Host = "127.0.0.1", Port = port, TimeoutMilliseconds = 1000 };
        ClientDescriptor descriptor = FindClient("mc-binary");
        ITransport transport = new RecordingTransport(settings.CreateTransport(TransportKind.Tcp), log);
        using PlcClient client = descriptor.CreateClient(transport, settings);

        PlcResult openResult = client.OpenAsync().GetAwaiter().GetResult();
        check("MC 연결", openResult.IsSuccess, openResult.IsSuccess ? descriptor.DisplayName : openResult.Error.Message);
        if (!openResult.IsSuccess)
        {
            return;
        }

        WriteOutcome singleBit = WriteExecutor.ExecuteAsync(
            client, "M101", WriteInterpretation.Bit, WriteMode.Block, new[] { "1" }, verify: true, CancellationToken.None)
            .GetAwaiter().GetResult();
        check("MC M101 비트 1점 쓰기", singleBit.IsSuccess, singleBit.Message);
        check("MC M101 검증", singleBit.IsVerified == true, singleBit.VerificationMessage);

        PlcResult<ReadOutcome> singleBitRead = ReadExecutor.ExecuteAsync(
            client, "M101", ReadInterpretation.Bit, CountUnit.Bit, 1, client.ByteOrder, CancellationToken.None)
            .GetAwaiter().GetResult();
        string singleBitText = singleBitRead.IsSuccess ? singleBitRead.Value.Rows[0].Value : singleBitRead.Error.Message;
        check("MC M101 비트 읽기", singleBitRead.IsSuccess && singleBitText == "1", singleBitText);

        WriteOutcome bitBlock = WriteExecutor.ExecuteAsync(
            client, "M100", WriteInterpretation.Bit, WriteMode.Block, new[] { "1", "0", "1" }, verify: true, CancellationToken.None)
            .GetAwaiter().GetResult();
        check("MC M100 3점 비트 쓰기", bitBlock.IsSuccess && bitBlock.IsVerified == true, bitBlock.VerificationMessage);

        PlcResult<ReadOutcome> bitBlockRead = ReadExecutor.ExecuteAsync(
            client, "M100", ReadInterpretation.Bit, CountUnit.Bit, 3, client.ByteOrder, CancellationToken.None)
            .GetAwaiter().GetResult();
        string bitBlockText = bitBlockRead.IsSuccess
            ? string.Join(",", bitBlockRead.Value.Rows.Select(row => row.Value))
            : bitBlockRead.Error.Message;
        check("MC M100 3점 읽기", bitBlockRead.IsSuccess && bitBlockText == "1,0,1", bitBlockText);

        WriteOutcome wordWrite = WriteExecutor.ExecuteAsync(
            client, "D100", WriteInterpretation.Word, WriteMode.Block, new[] { "0x1234", "4660" }, verify: true, CancellationToken.None)
            .GetAwaiter().GetResult();
        check("MC D100 워드 쓰기", wordWrite.IsSuccess && wordWrite.IsVerified == true, wordWrite.VerificationMessage);

        // M100 3점 쓰기(1,0,1)가 M101 을 0 으로 덮었으므로 그 결과까지 함께 확인한다.
        check(
            "MC 가상 메모리 반영",
            memory.GetBit(Mc3EDevice.M, 100) && !memory.GetBit(Mc3EDevice.M, 101) && memory.GetBit(Mc3EDevice.M, 102)
                && memory.GetWord(Mc3EDevice.D, 100) == 0x1234,
            $"M100 = {memory.GetBit(Mc3EDevice.M, 100)}, M101 = {memory.GetBit(Mc3EDevice.M, 101)}, "
                + $"M102 = {memory.GetBit(Mc3EDevice.M, 102)}, D100 = 0x{memory.GetWord(Mc3EDevice.D, 100):X4}");

        check("MC 패킷 기록", log.SentCount > 0 && log.ReceivedCount > 0, $"TX {log.SentCount} / RX {log.ReceivedCount}");

        client.CloseAsync().GetAwaiter().GetResult();
        server.Stop();
        check("MC 정지", !server.IsRunning, string.Empty);
    }

    private static int Finish(List<string> lines, int failures)
    {
        lines.Add(string.Empty);
        lines.Add(failures == 0 ? "결과: PASS" : $"결과: FAIL ({failures}건)");

        string path = Path.Combine(AppContext.BaseDirectory, "selftest-result.txt");
        File.WriteAllLines(path, lines);
        Console.WriteLine(string.Join(Environment.NewLine, lines));

        return failures == 0 ? 0 : 1;
    }

    private static int StartServer(VirtualPlcServer server, IVirtualPlcResponder responder, int firstPort)
    {
        for (int port = firstPort; port < firstPort + 20; port++)
        {
            try
            {
                server.Start(port, responder);
                return port;
            }
            catch (SocketException)
            {
            }
        }

        throw new InvalidOperationException("가상 PLC 가 쓸 수 있는 포트를 찾지 못했습니다.");
    }

    private static ClientDescriptor FindClient(string id)
    {
        foreach (ClientDescriptor descriptor in ClientCatalog.All)
        {
            if (descriptor.Id == id)
            {
                return descriptor;
            }
        }

        throw new InvalidOperationException($"클라이언트 '{id}'를 찾을 수 없습니다.");
    }
}
