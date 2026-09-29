using FieldLink.Communication;
using FieldLink.Communication.Udp;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.Hyundai.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.Hyundai.Servers
{
    /// <summary>현대 로봇의 온라인 추적 S/P/F 메시지를 받으며 위치 증분을 전송하는 UDP 서버입니다.</summary>
    public sealed class HyundaiUdpServer : ICommunicationServer
    {
        private readonly object sync = new object();
        private readonly UdpServer server;
        private readonly Dictionary<string, IPEndPoint> peers = new Dictionary<string, IPEndPoint>();
        private readonly int maximumPeers;
        private long peerGeneration;
        private byte[] lastPosition;
        private int sequence;
        /// <summary>수신 주소와 최대 상대 로봇 수를 지정합니다.</summary>
        public HyundaiUdpServer(IPEndPoint endpoint, int maximumPeers = 100)
        {
            if (maximumPeers < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumPeers));
            this.maximumPeers = maximumPeers;
            server = new UdpServer(endpoint, ReceiveAsync, 64);
        }
        /// <summary>P 위치 메시지 수신 알림입니다. 수신 순서대로 호출되며 핸들러는 짧게 실행하고 StopAsync를 기다리지 마세요.</summary>
        public event Action<HyundaiPositionCorrection> PositionReceived;
        /// <summary>마지막 이벤트 구독자 예외입니다. 구독자 예외는 수신을 중단하지 않습니다.</summary>
        public Exception LastObserverError { get; private set; }
        /// <summary>현재 실행 세대에서 받은 마지막 위치의 복사본입니다.</summary>
        public HyundaiPositionCorrection LastPosition { get { lock (sync) { RefreshGeneration(); return lastPosition == null || !server.IsRunning ? null : new HyundaiPositionCorrection(lastPosition); } } }
        /// <summary>실제로 바인딩한 주소입니다.</summary>
        public IPEndPoint LocalEndPoint => server.LocalEndPoint;
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
        private void RefreshGeneration()
        {
            long generation = server.Generation;
            if (generation == peerGeneration)
                return;
            peerGeneration = generation; peers.Clear(); lastPosition = null;
        }
        private async Task ReceiveAsync(byte[] packet, IPEndPoint remote)
        {
            var parsed = HyundaiProtocol.Parse(packet);
            if (!parsed.IsSuccess)
                return;
            lock (sync)
            {
                RefreshGeneration();
                string key = remote.ToString();
                if (!peers.ContainsKey(key) && peers.Count >= maximumPeers)
                    return;
                peers[key] = remote;
                if (parsed.Content.Command == 'P')
                    lastPosition = (byte[])packet.Clone();
            }
            if (parsed.Content.Command == 'S')
                await BroadcastAsync(HyundaiProtocol.BuildStartReply(packet)).ConfigureAwait(false);
            else if (parsed.Content.Command == 'P')
            {
                var observers = PositionReceived;
                if (observers != null)
                    foreach (Action<HyundaiPositionCorrection> observer in observers.GetInvocationList())
                {
                    try { observer(new HyundaiPositionCorrection(packet)); }
                    catch (Exception error) { LastObserverError = error; }
                }
            }
            // F는 원본과 같이 종료 통지로만 처리하고 해당 상대를 목록에서 삭제하지 않습니다.
        }
        /// <summary>등록된 상대에게 사용자 정의 메시지를 전송합니다.</summary>
        public Task<OperationResult> WriteAsync(HyundaiPositionCorrection data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Data == null || data.Data.Length != 6 || (data.CharDummy?.Length ?? 0) > 3)
                throw new ArgumentException("좌표는 6개, 예약 문자는 최대 3개여야 합니다.", nameof(data));
            return BroadcastAsync(data.ToBytes());
        }
        /// <summary>XYZ(mm)와 WPR(도) 위치 증분을 전송합니다.</summary>
        public Task<OperationResult> WriteIncrementAsync(double x, double y, double z, double w, double p, double r)
        {
            int count;
            lock (sync) { count = sequence; sequence = sequence == int.MaxValue ? 0 : sequence + 1; }
            return BroadcastAsync(HyundaiProtocol.BuildIncrement(count, x, y, z, w, p, r));
        }
        /// <summary>XYZ/WPR 순서의 6개 위치 증분을 전송합니다.</summary>
        public Task<OperationResult> WriteIncrementAsync(double[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Length != 6)
                throw new ArgumentException("위치 증분은 6개여야 합니다.", nameof(values));
            return WriteIncrementAsync(values[0], values[1], values[2], values[3], values[4], values[5]);
        }
        /// <summary>X축 위치를 mm 단위로 보정합니다.</summary>
        public Task<OperationResult> MoveXAsync(double value) => WriteIncrementAsync(value, 0, 0, 0, 0, 0);
        /// <summary>Y축 위치를 mm 단위로 보정합니다.</summary>
        public Task<OperationResult> MoveYAsync(double value) => WriteIncrementAsync(0, value, 0, 0, 0, 0);
        /// <summary>Z축 위치를 mm 단위로 보정합니다.</summary>
        public Task<OperationResult> MoveZAsync(double value) => WriteIncrementAsync(0, 0, value, 0, 0, 0);
        /// <summary>X축 회전을 도 단위로 보정합니다.</summary>
        public Task<OperationResult> RotateXAsync(double value) => WriteIncrementAsync(0, 0, 0, value, 0, 0);
        /// <summary>Y축 회전을 도 단위로 보정합니다.</summary>
        public Task<OperationResult> RotateYAsync(double value) => WriteIncrementAsync(0, 0, 0, 0, value, 0);
        /// <summary>Z축 회전을 도 단위로 보정합니다.</summary>
        public Task<OperationResult> RotateZAsync(double value) => WriteIncrementAsync(0, 0, 0, 0, 0, value);
        private async Task<OperationResult> BroadcastAsync(byte[] packet)
        {
            IPEndPoint[] targets;
            long generation;
            lock (sync)
            {
                RefreshGeneration(); generation = peerGeneration;
                if (!server.IsRunning)
                    return new OperationResult("먼저 서버를 시작하세요.");
                targets = peers.Values.ToArray();
            }
            if (targets.Length == 0)
                return new OperationResult("로봇의 첫 메시지를 기다리세요.");
            foreach (var target in targets)
            {
                if (generation != server.Generation || !server.IsRunning)
                    return new OperationResult("전송을 시작한 서버가 종료되었습니다.");
                await server.SendAsync(packet, target, generation).ConfigureAwait(false);
            }
            return OperationResult.CreateSuccessResult();
        }
    }
}
