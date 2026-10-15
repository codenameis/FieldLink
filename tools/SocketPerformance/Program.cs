using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldTcp = FieldLink.Communication.Tcp.TcpClient;
using FieldUdp = FieldLink.Communication.Udp.UdpClient;
using FieldTcpServer = FieldLink.Communication.Tcp.TcpServer;
using FieldUdpServer = FieldLink.Communication.Udp.UdpServer;

// Loopback only. Every timed response is checked byte-for-byte.
// BCL is a smaller-feature baseline: it has no FieldLink framing, diagnostics,
// connection generation, per-request cancellation or request serialization.
internal static class Program
{
    private const int Warmup = 1000;
    private const int Repeats = 3;
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 0 && args[0] == "--review-probes")
                return ReviewProbes.Run();
            AppDomain.MonitoringIsEnabled = true;
            if (args.Length == 2 && args[0] == "--readability")
            {
                ReadabilityPerformance.RunAsync(args[1]).GetAwaiter().GetResult();
                return 0;
            }
            Task work = RunAsync(args.Length == 0 ? "socket-performance.csv" : args[0]);
            if (!work.Wait(TimeSpan.FromMinutes(8)))
                throw new TimeoutException("Benchmark exceeded eight minutes.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(Path.ChangeExtension(output, ".environment.txt"),
            "UTC=" + DateTime.UtcNow.ToString("O") + Environment.NewLine +
            "OS=" + Environment.OSVersion + Environment.NewLine +
            "CLR=" + Environment.Version + Environment.NewLine +
            "Process64Bit=" + Environment.Is64BitProcess + Environment.NewLine +
            "LogicalProcessors=" + Environment.ProcessorCount + Environment.NewLine +
            "ServerGC=" + GCSettings.IsServerGC + Environment.NewLine +
            "WarmupPerConnection=" + Warmup + ";Repeats=" + Repeats + Environment.NewLine +
            "Loopback IPv4; persistent connections; one outstanding exchange per connection." + Environment.NewLine +
            "Connection setup, warmup and teardown excluded. Latencies include full response validation." + Environment.NewLine +
            "MiB/s counts request plus response payload; CPU cores and allocations include local echo peers." + Environment.NewLine +
            "TCP clients NoDelay=true; TCP servers retain framework defaults." + Environment.NewLine +
            "BCL baseline lacks FieldLink deadlines, diagnostics, framing and lifecycle guarantees." + Environment.NewLine);
        using (var outputFile = new StreamWriter(output))
        {
            outputFile.AutoFlush = true;
            outputFile.WriteLine("mode,bytes,connections,repeat,operations,seconds,ops_per_sec,duplex_mib_per_sec,p50_us,p95_us,p99_us,max_us,allocated_bytes_per_op,gc0,gc1,gc2,cpu_cores,errors");
            // Rotate modes each repeat to reduce fixed-order thermal/JIT bias.
            for (int repeat = 0; repeat < Repeats; repeat++)
            {
                foreach (bool udp in new[] { false, true })
                foreach (int size in udp ? new[] { 64, 1024, 8192 } : new[] { 64, 1024, 16384 })
                foreach (int connections in new[] { 1, 8 })
                {
                    for (int index = 0; index < 3; index++)
                    {
                        int mode = (index + repeat) % 3;
                        int count = udp
                            ? (connections == 1 ? (size <= 1024 ? 20000 : 15000) : (size <= 1024 ? 4000 : 3000))
                            : (connections == 1 ? (size <= 1024 ? 30000 : 20000) : (size <= 1024 ? 30000 : 10000));
                        string row = await MeasureAsync(udp, mode, size, connections, count, repeat + 1).ConfigureAwait(false);
                        outputFile.WriteLine(row);
                        Console.WriteLine(row);
                    }
                }
            }
        }
    }

    private static async Task<string> MeasureAsync(bool udp, int mode, int size, int concurrency, int count, int repeat)
    {
        using (var host = await EchoHost.StartAsync(udp, mode == 2, size, concurrency, count + Warmup).ConfigureAwait(false))
        {
            var clients = new List<ExchangeClient>();
            try
            {
                for (int index = 0; index < concurrency; index++)
                    clients.Add(await ExchangeClient.OpenAsync(udp, mode != 0, host.Endpoint, size).ConfigureAwait(false));
                var payloads = Enumerable.Range(0, concurrency).Select(index =>
                    Enumerable.Range(0, size).Select(offset => (byte)(offset * 31 + index)).ToArray()).ToArray();
                await Task.WhenAll(clients.Select((client, index) => ExerciseAsync(client, payloads[index], Warmup, null))).ConfigureAwait(false);
                var samples = clients.Select(client => new long[count]).ToArray();
                var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var workers = clients.Select((client, index) => MeasureWorkerAsync(start.Task, client, payloads[index], samples[index])).ToArray();
                // Count allocations over the whole AppDomain, not only one async continuation thread.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long allocatedBefore = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
                using (var process = Process.GetCurrentProcess())
                {
                    TimeSpan cpuBefore = process.TotalProcessorTime;
                    var elapsed = Stopwatch.StartNew();
                    start.SetResult(true);
                    await Task.WhenAll(workers).ConfigureAwait(false);
                    elapsed.Stop();
                    double cpuSeconds = (process.TotalProcessorTime - cpuBefore).TotalSeconds;
                    long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocatedBefore;
                    gc0 = GC.CollectionCount(0) - gc0;
                    gc1 = GC.CollectionCount(1) - gc1;
                    gc2 = GC.CollectionCount(2) - gc2;
                    await host.FinishAsync().ConfigureAwait(false);
                    long[] sorted = samples.SelectMany(value => value).OrderBy(value => value).ToArray();
                    int operations = sorted.Length;
                    double rate = operations / elapsed.Elapsed.TotalSeconds;
                    string label = (udp ? "udp_" : "tcp_") + (mode == 0 ? "bcl" : mode == 1 ? "field_client" : "field_pair");
                    return string.Join(",", new object[] { label, size, concurrency, repeat, operations,
                        F(elapsed.Elapsed.TotalSeconds), F(rate), F(rate * size * 2 / 1048576),
                        F(Percentile(sorted, 0.50)), F(Percentile(sorted, 0.95)), F(Percentile(sorted, 0.99)),
                        F(sorted[sorted.Length - 1] * 1000000.0 / Stopwatch.Frequency),
                        F((double)allocated / operations), gc0, gc1, gc2, F(cpuSeconds / elapsed.Elapsed.TotalSeconds), 0 });
                }
            }
            finally
            {
                foreach (var client in clients)
                    client.Dispose();
            }
        }
    }

    private static async Task MeasureWorkerAsync(Task start, ExchangeClient client, byte[] payload, long[] samples)
    {
        await start.ConfigureAwait(false);
        await ExerciseAsync(client, payload, samples.Length, samples).ConfigureAwait(false);
    }

    private static async Task ExerciseAsync(ExchangeClient client, byte[] payload, int count, long[] samples)
    {
        for (int index = 0; index < count; index++)
        {
            // Vary identity on every round trip so a stale echo cannot pass validation.
            payload[0] = (byte)index;
            payload[1] = (byte)(index >> 8);
            long started = Stopwatch.GetTimestamp();
            byte[] response = await client.Exchange(payload).ConfigureAwait(false);
            if (response.Length != payload.Length)
                throw new InvalidDataException("Wrong echo length.");
            for (int offset = 0; offset < payload.Length; offset++)
            {
                if (response[offset] != payload[offset])
                    throw new InvalidDataException("Wrong echo content.");
            }
            if (samples != null)
                samples[index] = Stopwatch.GetTimestamp() - started;
        }
    }

    private static string F(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
    private static double Percentile(long[] samples, double percentile) =>
        samples[(int)Math.Ceiling(samples.Length * percentile) - 1] * 1000000.0 / Stopwatch.Frequency;

    private sealed class ExchangeClient : IDisposable
    {
        private readonly IDisposable owner;
        internal readonly Func<byte[], Task<byte[]>> Exchange;
        private ExchangeClient(IDisposable owner, Func<byte[], Task<byte[]>> exchange)
        {
            this.owner = owner;
            Exchange = exchange;
        }
        internal static async Task<ExchangeClient> OpenAsync(bool udp, bool field, IPEndPoint endpoint, int size)
        {
            if (field)
            {
                ICommunicationClient client = udp
                    ? (ICommunicationClient)new FieldUdp(endpoint, timeout: TimeSpan.FromSeconds(5))
                    : new FieldTcp(endpoint, new FixedLengthFrame(size), timeout: TimeSpan.FromSeconds(5));
                try
                {
                    await client.OpenAsync().ConfigureAwait(false);
                    return new ExchangeClient(client, payload => client.ExchangeAsync(payload));
                }
                catch
                {
                    client.Dispose();
                    throw;
                }
            }
            if (udp)
            {
                var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                client.Connect(endpoint);
                return new ExchangeClient(client, async payload =>
                {
                    await client.SendAsync(payload, payload.Length).ConfigureAwait(false);
                    return (await client.ReceiveAsync().ConfigureAwait(false)).Buffer;
                });
            }
            var tcp = new TcpClient { NoDelay = true };
            await tcp.ConnectAsync(endpoint.Address, endpoint.Port).ConfigureAwait(false);
            NetworkStream stream = tcp.GetStream();
            return new ExchangeClient(tcp, async payload =>
            {
                await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
                byte[] response = new byte[size];
                await ReadExactlyAsync(stream, response).ConfigureAwait(false);
                return response;
            });
        }
        public void Dispose() => owner.Dispose();
    }

    private sealed class EchoHost : IDisposable
    {
        private TcpListener listener;
        private UdpClient udp;
        private FieldTcpServer fieldTcp;
        private FieldUdpServer fieldUdp;
        private readonly List<TcpClient> accepted = new List<TcpClient>();
        private Task completion = Task.CompletedTask;
        internal IPEndPoint Endpoint { get; private set; }
        internal static async Task<EchoHost> StartAsync(bool isUdp, bool field, int size, int clients, int count)
        {
            var host = new EchoHost();
            if (isUdp)
            {
                if (field)
                {
                    host.fieldUdp = new FieldUdpServer(new IPEndPoint(IPAddress.Loopback, 0),
                        (data, remote) => host.fieldUdp.SendAsync(data, remote));
                    await host.fieldUdp.StartAsync().ConfigureAwait(false);
                    host.Endpoint = host.fieldUdp.LocalEndPoint;
                }
                else
                {
                    host.udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                    host.Endpoint = (IPEndPoint)host.udp.Client.LocalEndPoint;
                    host.completion = Task.Run(async () =>
                    {
                        for (int index = 0; index < clients * count; index++)
                        {
                            var packet = await host.udp.ReceiveAsync().ConfigureAwait(false);
                            await host.udp.SendAsync(packet.Buffer, packet.Buffer.Length, packet.RemoteEndPoint).ConfigureAwait(false);
                        }
                    });
                }
            }
            else if (field)
            {
                host.fieldTcp = new FieldTcpServer(new IPEndPoint(IPAddress.Loopback, 0), async (client, token) =>
                {
                    var boundary = new FixedLengthFrame(size);
                    for (int index = 0; index < count; index++)
                        await client.SendAsync(await client.ReceiveAsync(boundary).ConfigureAwait(false)).ConfigureAwait(false);
                });
                await host.fieldTcp.StartAsync().ConfigureAwait(false);
                host.Endpoint = host.fieldTcp.LocalEndPoint;
            }
            else
            {
                host.listener = new TcpListener(IPAddress.Loopback, 0);
                host.listener.Start();
                host.Endpoint = (IPEndPoint)host.listener.LocalEndpoint;
                host.completion = host.AcceptAsync(clients, count, size);
            }
            return host;
        }
        private async Task AcceptAsync(int clients, int count, int size)
        {
            var workers = new List<Task>();
            for (int index = 0; index < clients; index++)
            {
                var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                lock (accepted)
                    accepted.Add(client);
                workers.Add(EchoTcpAsync(client, count, size));
            }
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        private static async Task EchoTcpAsync(TcpClient client, int count, int size)
        {
            var buffer = new byte[size];
            NetworkStream stream = client.GetStream();
            for (int index = 0; index < count; index++)
            {
                await ReadExactlyAsync(stream, buffer).ConfigureAwait(false);
                await stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            }
        }
        internal async Task FinishAsync()
        {
            await completion.ConfigureAwait(false);
            if (fieldTcp != null)
            {
                await fieldTcp.StopAsync().ConfigureAwait(false);
                if (fieldTcp.LastConnectionError != null)
                    throw new IOException("FieldLink echo server failed.", fieldTcp.LastConnectionError);
            }
            if (fieldUdp != null)
                await fieldUdp.StopAsync().ConfigureAwait(false);
        }
        public void Dispose()
        {
            fieldTcp?.Dispose();
            fieldUdp?.Dispose();
            udp?.Dispose();
            listener?.Stop();
            lock (accepted)
            {
                foreach (var client in accepted)
                    client.Dispose();
            }
        }
    }

    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer)
    {
        int received = 0;
        while (received < buffer.Length)
        {
            int count = await stream.ReadAsync(buffer, received, buffer.Length - received).ConfigureAwait(false);
            if (count == 0)
                throw new EndOfStreamException("Echo peer closed early.");
            received += count;
        }
    }
}
