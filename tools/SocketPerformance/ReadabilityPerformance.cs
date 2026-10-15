using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FieldLink.PlcDrivers.Common;
using FieldLink.Secs;
using FieldLink.Secs.Types;

// Uses the same harness for the saved baseline and the current product assembly.
// HSMS runs on loopback only. Every request checks the returned protocol response.
internal static class ReadabilityPerformance
{
    private const int Repeats = 5;
    private const int HsmsWarmup = 500;
    private const int HsmsRequestsPerConnection = 30000;
    private const int CommonIterations = 5000000;

    internal static async Task RunAsync(string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
        using (var output = new StreamWriter(outputPath))
        {
            output.AutoFlush = true;
            output.WriteLine("mode,connections,repeat,operations,seconds,ops_per_sec,p95_us,p99_us,allocated_bytes_per_op,errors");
            for (int repeat = 1; repeat <= Repeats; repeat++)
            {
                foreach (int connections in new[] { 1, 8 })
                {
                    string row = await MeasureHsmsAsync(connections, repeat).ConfigureAwait(false);
                    output.WriteLine(row);
                    Console.WriteLine(row);
                }

                string commonRow = MeasureCommon(repeat);
                output.WriteLine(commonRow);
                Console.WriteLine(commonRow);
            }
        }
    }

    private static async Task<string> MeasureHsmsAsync(int connections, int repeat)
    {
        using (var server = new HsmsServer(new IPEndPoint(IPAddress.Loopback, 0), automaticGemReplies: true))
        {
            await server.StartAsync().ConfigureAwait(false);
            var clients = new List<HsmsClient>();
            try
            {
                for (int index = 0; index < connections; index++)
                {
                    var client = new HsmsClient(server.LocalEndPoint);
                    clients.Add(client);
                    await client.OpenAsync().ConfigureAwait(false);
                }

                await Task.WhenAll(clients.Select(client => ExerciseHsmsAsync(client, HsmsWarmup, null))).ConfigureAwait(false);
                long[][] samples = clients.Select(client => new long[HsmsRequestsPerConnection]).ToArray();
                var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Task[] workers = clients.Select((client, index) => RunWorkerAsync(start.Task, client, samples[index])).ToArray();
                CollectGarbage();
                long allocatedBefore = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var elapsed = Stopwatch.StartNew();
                start.SetResult(true);
                await Task.WhenAll(workers).ConfigureAwait(false);
                elapsed.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocatedBefore;
                long[] sortedSamples = samples.SelectMany(values => values).OrderBy(value => value).ToArray();
                int operations = connections * HsmsRequestsPerConnection;
                return Row("hsms_gem", connections, repeat, operations, elapsed.Elapsed.TotalSeconds,
                    Percentile(sortedSamples, 0.95), Percentile(sortedSamples, 0.99), allocated);
            }
            finally
            {
                foreach (HsmsClient client in clients)
                    client.Dispose();
                await server.StopAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task RunWorkerAsync(Task start, HsmsClient client, long[] samples)
    {
        await start.ConfigureAwait(false);
        await ExerciseHsmsAsync(client, samples.Length, samples).ConfigureAwait(false);
    }

    private static async Task ExerciseHsmsAsync(HsmsClient client, int count, long[] samples)
    {
        for (int index = 0; index < count; index++)
        {
            long started = Stopwatch.GetTimestamp();
            // The built-in peer returns an empty SECS list for S1F1.
            // HSMS correlates system bytes; this check additionally validates the payload.
            SecsMessage response = await client.Session.RequestBytesAsync(1, 1, Array.Empty<byte>()).ConfigureAwait(false);
            byte[] responseData = response.Data;
            if (response.StreamNo != 1 || response.FunctionNo != 2 || responseData.Length != 2 ||
                responseData[0] != 1 || responseData[1] != 0)
                throw new InvalidDataException("Unexpected S1F2 response.");
            if (samples != null)
                samples[index] = Stopwatch.GetTimestamp() - started;
        }
    }

    private static string MeasureCommon(int repeat)
    {
        byte[] payload = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
        OperationResult<byte[]> input = OperationResult.CreateSuccessResult(payload);
        Func<byte[], int> translator = bytes => bytes[0] + bytes[63];
        for (int index = 0; index < 10000; index++)
            ExerciseCommon(payload, input, translator);

        CollectGarbage();
        long allocatedBefore = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        var elapsed = Stopwatch.StartNew();
        for (int index = 0; index < CommonIterations; index++)
            ExerciseCommon(payload, input, translator);
        elapsed.Stop();
        long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocatedBefore;
        return Row("common_split_convert", 0, repeat, CommonIterations, elapsed.Elapsed.TotalSeconds, 0, 0, allocated);
    }

    private static void ExerciseCommon(byte[] payload, OperationResult<byte[]> input, Func<byte[], int> translator)
    {
        int[] lengths = ProtocolBytes.SplitIntegerToArray(65, 16);
        List<byte[]> chunks = ProtocolBytes.ArraySplitByLength(payload, 16);
        var reads = AddressParameters.SplitReadLength(100, 65, 16);
        var writes = AddressParameters.SplitWriteData(100, payload, 8, 2);
        var converted = ProtocolConversions.GetResultFromBytes(input, translator);
        if (lengths.Length != 5 || lengths[4] != 1 || chunks.Count != 4 || chunks[3][15] != 63 ||
            !reads.IsSuccess || reads.Content1[4] != 164 || !writes.IsSuccess || writes.Content1[3] != 124 ||
            writes.Content2[3][15] != 63 || !converted.IsSuccess || converted.Content != 63)
            throw new InvalidDataException("Common operation changed its result.");
    }

    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static double Percentile(long[] samples, double fraction)
    {
        int index = (int)Math.Ceiling(samples.Length * fraction) - 1;
        return samples[index] * 1000000.0 / Stopwatch.Frequency;
    }

    private static string Row(string mode, int connections, int repeat, int operations,
        double seconds, double p95, double p99, long allocated)
    {
        return string.Join(",", mode, connections, repeat, operations, Format(seconds),
            Format(operations / seconds), Format(p95), Format(p99), Format((double)allocated / operations), "0");
    }

    private static string Format(double value)
    {
        return value.ToString("F3", CultureInfo.InvariantCulture);
    }
}
