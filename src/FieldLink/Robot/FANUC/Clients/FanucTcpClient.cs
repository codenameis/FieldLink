using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Internal;
using FieldLink.Communication.Tcp;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.Robot.FANUC.Protocols;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.FANUC.Clients
{
    /// <summary>FANUC 초기화·메모리·상태·자세 교환을 수행합니다. 초기화와 상태 캐시는 현재 연결에만 유효합니다.</summary>
    public sealed class FanucTcpClient
    {
        private readonly TcpProtocolSession<Session> session;
        private readonly TimeSpan timeout;
        private readonly Encoding encoding;
        private readonly TimeSpan retainTime;
        private static readonly IFrameBoundary Boundary = new HeaderLengthFrame(56, bytes => 56 + BitConverter.ToUInt16(bytes.Array, bytes.Offset + 4));
        /// <summary>호출자가 소유하는 TCP 전송과 초기화 ID·문자 인코딩·캐시 유지 시간을 지정합니다.</summary>
        public FanucTcpClient(ITcpClient transport, int clientId = 1024, Encoding encoding = null, TimeSpan? timeout = null, TimeSpan? retainTime = null)
        {
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));
            this.encoding = encoding ?? Encoding.Default; this.timeout = timeout ?? TimeSpan.FromSeconds(10);
            ClientOperation.ValidateTimeout(this.timeout); this.retainTime = retainTime ?? TimeSpan.FromMilliseconds(100);
            if (this.retainTime < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(retainTime));
            session = new TcpProtocolSession<Session>(transport, async tx =>
            {
                /* 이전 구현 — R-016
                foreach (byte[] command in FanucProtocol.BuildInitialization(clientId))
                    await tx.ExchangeAsync(command, Boundary).ConfigureAwait(false);
                수신 성공만으로 세션을 만들면 G 할당 거절도 초기화 완료가 된다.
                */
                byte[][] commands = FanucProtocol.BuildInitialization(clientId);
                for (int step = 0; step < commands.Length; step++)
                {
                    byte[] response = await tx.ExchangeAsync(commands[step], Boundary).ConfigureAwait(false);
                    var accepted = FanucInitializationResponseParser.Parse(step, commands[step], response);
                    if (!accepted.IsSuccess)
                    {
                        var error = new System.IO.InvalidDataException($"FANUC 초기화 {step + 1}단계 실패: {accepted.Message} (코드 {accepted.ErrorCode})");
                        error.Data["ErrorCode"] = accepted.ErrorCode;
                        error.Data["InitializationStep"] = step;
                        throw error;
                    }
                }
                return new Session();
            });
        }
        /// <summary>현재 연결의 초기화 완료 여부입니다.</summary>
        public bool IsInitialized => session.IsInitialized;
        /// <summary>연결 초기화를 명시적으로 완료합니다. 명령도 필요하면 같은 초기화를 자동 수행합니다.</summary>
        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken)) => session.ExecuteAsync((tx, value) => Task.FromResult(true), timeout, cancellationToken);
        /// <summary>D·AI·AQ와 해당 별칭의 워드 영역을 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            var command = FanucProtocol.BuildRead(address ?? throw new ArgumentNullException(nameof(address)), length);
            return command.IsSuccess ? ReadPacketAsync(command.Content, length, cancellationToken) : Task.FromResult(command);
        }
        /// <summary>원시 선택자와 1 기반 주소로 워드 영역을 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(byte selector, ushort address, ushort length, CancellationToken cancellationToken = default(CancellationToken)) => ReadPacketAsync(FanucProtocol.BuildReadData(selector, address, length), length, cancellationToken);
        private Task<OperationResult<byte[]>> ReadPacketAsync(byte[] packet, ushort length, CancellationToken token) =>
            session.ExecuteAsync(async (tx, value) => FanucProtocol.ParseReadResponse(await tx.ExchangeAsync(packet, Boundary).ConfigureAwait(false), length), timeout, token);
        /// <summary>전체 로봇 상태 영역 D1의 6130워드를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(CancellationToken cancellationToken = default(CancellationToken)) => ReadAsync(FanucProtocol.SELECTOR_D, 1, 6130, cancellationToken);
        /// <summary>GI·GO 등을 부호 없는 16비트 값으로 읽습니다.</summary>
        public async Task<OperationResult<ushort[]>> ReadUInt16Async(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            var read = await ReadAsync(address, length, cancellationToken).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(new ProtocolValueConverter().ReadUInt16(read.Content, 0, length)) : read.ConvertFailed<ushort[]>();
        }
        /// <summary>GI·GO 등의 워드 영역에 16비트 값들을 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, ushort[] value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WriteAsync(address, new ProtocolValueConverter().GetBytes(value ?? throw new ArgumentNullException(nameof(value))), cancellationToken);
        /// <summary>선택자와 1 기반 주소로 비트를 읽습니다. PMC R2는 M 선택자를 사용합니다.</summary>
        public Task<OperationResult<bool[]>> ReadBoolAsync(byte selector, ushort address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            byte[] command = FanucProtocol.BuildReadBits(selector, address, length);
            return session.ExecuteAsync(async (tx, value) => FanucProtocol.ParseBitResponse(await tx.ExchangeAsync(command, Boundary).ConfigureAwait(false), address, length), timeout, cancellationToken);
        }
        /// <summary>선택자와 1 기반 주소로 비트를 기록합니다.</summary>
        public Task<OperationResult> WriteBoolAsync(byte selector, ushort address, bool[] value, CancellationToken cancellationToken = default(CancellationToken)) =>
            WritePacketsAsync(new[] { FanucProtocol.BuildWriteBits(selector, address, value ?? throw new ArgumentNullException(nameof(value))) }, true, cancellationToken);
        /// <summary>선택자와 1 기반 주소로 워드 바이트를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(byte selector, ushort address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return WritePacketsAsync(new[] { FanucProtocol.BuildWriteData(selector, address, value, value.Length / 2) }, false, cancellationToken);
        }
        /// <summary>비트 영역과 SDO·SDI·RDI·RDO·UI·UO·SI·SO 별칭을 읽습니다.</summary>
        public Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken cancellationToken = default(CancellationToken))
        {
            var command = FanucProtocol.BuildRead(address ?? throw new ArgumentNullException(nameof(address)), length, true);
            if (!command.IsSuccess)
                return Task.FromResult(OperationResult.CreateFailedResult<bool[]>(command));
            ushort start = FanucProtocol.ParseAddress(address, true).Content2;
            return session.ExecuteAsync(async (tx, value) => FanucProtocol.ParseBitResponse(await tx.ExchangeAsync(command.Content, Boundary).ConfigureAwait(false), start, length), timeout, cancellationToken);
        }
        /// <summary>워드 영역에 원시 바이트를 씁니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var packet = FanucProtocol.BuildWrite(address ?? throw new ArgumentNullException(nameof(address)), value);
            return packet.IsSuccess ? WritePacketsAsync(new[] { packet.Content }, false, cancellationToken) : Task.FromResult<OperationResult>(packet);
        }
        /// <summary>비트 영역에 접점 값을 씁니다.</summary>
        public Task<OperationResult> WriteAsync(string address, bool[] value, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var packet = FanucProtocol.BuildWrite(address ?? throw new ArgumentNullException(nameof(address)), value);
            return packet.IsSuccess ? WritePacketsAsync(new[] { packet.Content }, true, cancellationToken) : Task.FromResult<OperationResult>(packet);
        }
        /// <summary>위치·자세·구성·사용자 좌표·공구 값을 순서대로 씁니다. 실패 이후 단계는 실행하지 않습니다.</summary>
        public Task<OperationResult> WriteXyzwprAsync(ushort address, float[] position, short[] config, short userFrame, short userTool, CancellationToken cancellationToken = default(CancellationToken)) =>
            WritePacketsAsync(FanucProtocol.BuildWriteXyzwpr(address, position, config, userFrame, userTool), false, cancellationToken);
        /// <summary>관절 좌표와 프레임·공구 값을 순서대로 씁니다.</summary>
        public Task<OperationResult> WriteJointAsync(ushort address, float[] joint, short userFrame, short userTool, CancellationToken cancellationToken = default(CancellationToken)) =>
            WritePacketsAsync(FanucProtocol.BuildWriteJoint(address, joint, userFrame, userTool), false, cancellationToken);
        private Task<OperationResult> WritePacketsAsync(byte[][] packets, bool bits, CancellationToken token) => session.ExecuteAsync(async (tx, value) =>
        {
            foreach (byte[] packet in packets)
            {
                var check = FanucProtocol.ParseWriteResponse(await tx.ExchangeAsync(packet, Boundary).ConfigureAwait(false), bits);
                if (!check.IsSuccess)
                    return check;
            }
            return OperationResult.CreateSuccessResult();
        }, timeout, token);
        /// <summary>전체 상태 프레임을 모델로 해석합니다.</summary>
        public async Task<OperationResult<FanucControllerSnapshot>> ReadDataAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var read = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return read.IsSuccess ? FanucSnapshotParser.Parse(read.Content, encoding) : OperationResult.CreateFailedResult<FanucControllerSnapshot>(read);
        }
        /// <summary>전체 상태 또는 지정한 모델 속성을 JSON으로 읽습니다. 속성 조회 캐시는 연결마다 새로 시작합니다.</summary>
        public Task<OperationResult<string>> ReadStringAsync(string property = null, CancellationToken cancellationToken = default(CancellationToken)) => session.ExecuteAsync(async (tx, value) =>
        {
            if (string.IsNullOrEmpty(property) || value.Data == null || value.Age.Elapsed > retainTime)
            {
                var read = FanucProtocol.ParseReadResponse(await tx.ExchangeAsync(FanucProtocol.BuildReadData(FanucProtocol.SELECTOR_D, 1, 6130), Boundary).ConfigureAwait(false), 6130);
                if (!read.IsSuccess)
                    return OperationResult.CreateFailedResult<string>(read);
                var parsed = FanucSnapshotParser.Parse(read.Content, encoding);
                if (!parsed.IsSuccess)
                    return OperationResult.CreateFailedResult<string>(parsed);
                value.Data = parsed.Content; value.Age.Restart();
            }
            object content = value.Data;
            if (!string.IsNullOrEmpty(property))
            {
                PropertyInfo info = typeof(FanucControllerSnapshot).GetProperty(property);
                if (info == null)
                    return new OperationResult<string>("지원하지 않는 데이터 속성입니다.");
                content = info.GetValue(value.Data, null);
            }
            return OperationResult.CreateSuccessResult(JsonConvert.SerializeObject(content, Formatting.Indented));
        }, timeout, cancellationToken);
        private sealed class Session
        {
            internal FanucControllerSnapshot Data;
            internal readonly Stopwatch Age = new Stopwatch();
        }
    }
}
