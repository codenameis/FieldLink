using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Tcp;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.FANUC.Protocols;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.FANUC.Servers
{
    /// <summary>FANUC 로봇의 I·Q·M·AI·AQ·D 메모리와 연결 협상을 제공하는 가상 TCP 서버입니다.</summary>
    public sealed class FanucTcpServer : ICommunicationServer
    {
        private const int PoolLength = 65536;
        private readonly object memoryLock = new object();
        private readonly byte[][] memory = Enumerable.Range(0, 6).Select(i => new byte[PoolLength]).ToArray();
        private readonly TcpServer server;
        private volatile bool enableWrite = true;
        private static readonly IFrameBoundary Boundary = new HeaderLengthFrame(56, h => 56 + BitConverter.ToUInt16(h.Array, h.Offset + 4));
        /// <summary>수신 주소를 지정하고 참조 구현의 로봇 상태 샘플을 D 영역에 적재합니다. 실제 수신은 StartAsync로 시작합니다.</summary>
        public FanucTcpServer(IPEndPoint endpoint, TimeSpan? ioTimeout = null)
        {
            using (Stream sample = typeof(FanucTcpServer).Assembly.GetManifestResourceStream("FieldLink.Robot.FANUC.Servers.FanucSampleData.bin"))
            {
                if (sample == null)
                    throw new InvalidOperationException("FANUC 상태 샘플 리소스가 없습니다.");
                int offset = 0, count;
                while ((count = sample.Read(memory[5], offset, memory[5].Length - offset)) != 0) offset += count;
            }
            server = new TcpServer(endpoint, HandleAsync, maximumFrameLength: 65591, ioTimeout: ioTimeout);
        }
        /// <summary>외부 쓰기를 허용합니다. 비허용 시 쓰기 요청 연결을 종료하며 메모리를 변경하지 않습니다. 로컬 Write는 영향을 받지 않습니다.</summary>
        public bool EnableWrite { get => enableWrite; set => enableWrite = value; }
        /// <summary>실제로 바인딩한 주소입니다.</summary>
        public IPEndPoint LocalEndPoint => server.LocalEndPoint;
        /// <summary>마지막 접속 처리 오류입니다.</summary>
        public Exception LastConnectionError => server.LastConnectionError;
        /// <inheritdoc />
        public bool IsRunning => server.IsRunning;
        /// <inheritdoc />
        public Task Completion => server.Completion;
        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken)) => server.StartAsync(cancellationToken);
        /// <inheritdoc />
        public Task StopAsync() => server.StopAsync();
        /// <inheritdoc />
        public void Dispose() => server.Dispose();
        private async Task HandleAsync(ITcpServerConnection connection, CancellationToken token)
        {
            await connection.ReceiveAsync(Boundary).ConfigureAwait(false);
            await connection.SendAsync(FanucServerProtocol.BuildConnectReply()).ConfigureAwait(false);
            await connection.ReceiveAsync(Boundary).ConfigureAwait(false);
            await connection.SendAsync(FanucServerProtocol.BuildSessionReply()).ConfigureAwait(false);
            while (!token.IsCancellationRequested)
            {
                byte[] packet = await connection.ReceiveAsync(Boundary).ConfigureAwait(false);
                var parsed = FanucServerProtocol.ParseRequest(packet);
                if (!parsed.IsSuccess)
                    throw new InvalidDataException(parsed.Message);
                FanucServerRequest request = parsed.Content;
                byte[] reply;
                if (request.Selector == FanucProtocol.SELECTOR_G)
                    reply = FanucServerProtocol.BuildWriteReply(request.Function, true);
                else
                {
                    int pool = GetPool(request.Selector);
                    int offset = request.Address * (request.IsWord ? 2 : 1);
                    int length = request.Length * (request.IsWord ? 2 : 1);
                    lock (memoryLock)
                    {
                        if (pool >= 0 && (offset > PoolLength || length > PoolLength - offset))
                            throw new InvalidDataException("FANUC 메모리 범위를 벗어났습니다.");
                        if (request.Function == 6)
                        {
                            byte[] data = pool < 0 ? new byte[0] : memory[pool].SelectMiddle(offset, length);
                            if (!request.IsWord)
                                data = ProtocolBytes.BoolArrayToByte(data.Select(b => b != 0).ToArray());
                            reply = FanucProtocol.BuildReadResponseData(data);
                        }
                        else
                        {
                            if (!EnableWrite)
                                return;
                            if (pool >= 0)
                            {
                                if (request.Data.Length != length)
                                    throw new InvalidDataException("FANUC 쓰기 데이터 길이가 부족합니다.");
                                Buffer.BlockCopy(request.Data, 0, memory[pool], offset, length);
                            }
                            reply = FanucServerProtocol.BuildWriteReply(request.Function);
                        }
                    }
                }
                await connection.SendAsync(reply).ConfigureAwait(false);
            }
        }
        /// <summary>서버의 워드 메모리를 1 기반 주소로 읽습니다.</summary>
        public OperationResult<byte[]> Read(string address, ushort length) => ReadLocal(address, length, false);
        /// <summary>서버의 비트 메모리를 1 기반 주소로 읽습니다.</summary>
        public OperationResult<bool[]> ReadBool(string address, ushort length)
        {
            var read = ReadLocal(address, length, true);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(read.Content.Select(b => b != 0).ToArray()) : read.ConvertFailed<bool[]>();
        }
        /// <summary>서버의 워드 메모리를 설정합니다.</summary>
        public OperationResult Write(string address, byte[] value) => WriteLocal(address, value, false);
        /// <summary>서버의 비트 메모리를 설정합니다.</summary>
        public OperationResult Write(string address, bool[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return WriteLocal(address, value.Select(b => b ? (byte)1 : (byte)0).ToArray(), true);
        }
        private OperationResult<byte[]> ReadLocal(string address, ushort length, bool bits)
        {
            var parsed = FanucProtocol.ParseAddress(address, bits);
            if (!parsed.IsSuccess)
                return parsed.ConvertFailed<byte[]>();
            int pool = GetPool(parsed.Content1), offset = (parsed.Content2 - 1) * (bits ? 1 : 2), count = length * (bits ? 1 : 2);
            if (pool < 0 || (bits ? pool > 2 : pool < 3) || offset < 0 || count > PoolLength - offset)
                return new OperationResult<byte[]>("지원하지 않는 영역이거나 메모리 범위를 벗어났습니다.");
            lock (memoryLock) return OperationResult.CreateSuccessResult(memory[pool].SelectMiddle(offset, count));
        }
        private OperationResult WriteLocal(string address, byte[] value, bool bits)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var parsed = FanucProtocol.ParseAddress(address, bits);
            if (!parsed.IsSuccess)
                return parsed;
            int pool = GetPool(parsed.Content1), offset = (parsed.Content2 - 1) * (bits ? 1 : 2);
            if (pool < 0 || (bits ? pool > 2 : pool < 3) || offset < 0 || value.Length > PoolLength - offset)
                return new OperationResult("지원하지 않는 영역이거나 메모리 범위를 벗어났습니다.");
            lock (memoryLock) Buffer.BlockCopy(value, 0, memory[pool], offset, value.Length);
            return OperationResult.CreateSuccessResult();
        }
        /// <summary>I·Q·M·AI·AQ·D 순서로 각 65536바이트를 복사한 스냅샷을 반환합니다.</summary>
        public byte[] SaveSnapshot()
        {
            lock (memoryLock)
            {
                byte[] result = new byte[PoolLength * 6];
                for (int i = 0; i < 6; i++) Buffer.BlockCopy(memory[i], 0, result, PoolLength * i, PoolLength);
                return result;
            }
        }
        /// <summary>스냅샷의 여섯 메모리 영역을 한 번에 복원합니다.</summary>
        public void LoadSnapshot(byte[] snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Length < PoolLength * 6)
                throw new ArgumentException("스냅샷 길이가 부족합니다.", nameof(snapshot));
            lock (memoryLock) for (int i = 0; i < 6; i++) Buffer.BlockCopy(snapshot, PoolLength * i, memory[i], 0, PoolLength);
        }
        private static int GetPool(byte selector)
        {
            switch (selector)
            {
                case FanucProtocol.SELECTOR_I: return 0;
                case FanucProtocol.SELECTOR_Q: return 1;
                case FanucProtocol.SELECTOR_M: return 2;
                case FanucProtocol.SELECTOR_AI: return 3;
                case FanucProtocol.SELECTOR_AQ: return 4;
                case FanucProtocol.SELECTOR_D: return 5;
                default: return -1;
            }
        }
    }
}
