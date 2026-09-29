using FieldLink.Communication;
using FieldLink.Communication.Framing;
using FieldLink.Communication.Serial;
using FieldLink.PlcDrivers.Common.Clients;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Siemens;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.PlcDrivers.Siemens.Clients
{
    /// <summary>참조 구현의 MPI 토큰 협상과 확인 절차를 제공합니다. 이 프로토콜은 실물 장비 검증 전입니다.</summary>
    public sealed class SiemensMpiSerialClient
    {
        private readonly SerialProtocolSession<object> session;
        private readonly IFrameBoundary responseBoundary;
        private readonly TimeSpan timeout;
        private readonly byte station;
        /// <summary>전송과 본문 수신 경계를 지정합니다. 기본 본문 경계는 원본 수신 방식에 대응하는 40ms 무수신 간격입니다.</summary>
        public SiemensMpiSerialClient(ISerialClient transport, byte station = 2, IFrameBoundary responseBoundary = null, TimeSpan? timeout = null)
        {
            this.station = station;
            this.responseBoundary = responseBoundary ?? new IdleGapFrame(TimeSpan.FromMilliseconds(40));
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            session = new SerialProtocolSession<object>(transport, HandshakeCoreAsync);
        }
        private static async Task<object> HandshakeCoreAsync(ISerialTransaction tx)
        {
            while (true)
            {
                byte[] received = await tx.ReceiveAsync(new FixedLengthFrame(3)).ConfigureAwait(false);
                byte[] reply = MpiSessionCodec.BuildHandshakeReply(received);
                if (reply == null)
                    continue;
                await tx.SendAsync(reply).ConfigureAwait(false);
                if (received[1] == 0)
                    return new object();
            }
        }
        /// <summary>현재 연결의 토큰 협상을 완료합니다. 새 연결의 첫 명령에서도 자동으로 같은 협상을 수행합니다.</summary>
        public Task<OperationResult> HandshakeAsync(CancellationToken token = default(CancellationToken)) => session.ExecuteAsync((tx, state) => Task.FromResult(OperationResult.CreateSuccessResult()), timeout, token);
        /// <summary>연속 바이트를 읽습니다.</summary>
        public Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken token = default(CancellationToken)) =>
            ExecuteAsync(SiemensMPICommandBuilder.BuildReadCommand(station, address, length, false), false, r => MpiSessionCodec.ParseRead(r, length), token);
        /// <summary>연속 비트를 읽습니다.</summary>
        public Task<OperationResult<bool[]>> ReadBoolAsync(string address, ushort length, CancellationToken token = default(CancellationToken)) =>
            ExecuteAsync(SiemensMPICommandBuilder.BuildReadCommand(station, address, length, true), false, r => MpiSessionCodec.ParseReadBits(r, length), token);
        /// <summary>연속 바이트를 기록합니다.</summary>
        public async Task<OperationResult> WriteAsync(string address, byte[] value, CancellationToken token = default(CancellationToken)) =>
            await ExecuteAsync(SiemensMPICommandBuilder.BuildWriteCommand(station, address, value), true, r => OperationResult.CreateSuccessResult(true), token).ConfigureAwait(false);
        /// <summary>바이트 하나를 읽습니다.</summary>
        public async Task<OperationResult<byte>> ReadByteAsync(string address, CancellationToken token = default(CancellationToken))
        {
            var read = await ReadAsync(address, 1, token).ConfigureAwait(false);
            return read.IsSuccess ? OperationResult.CreateSuccessResult(read.Content[0]) : read.ConvertFailed<byte>();
        }
        /// <summary>바이트 하나를 기록합니다.</summary>
        public Task<OperationResult> WriteAsync(string address, byte value, CancellationToken token = default(CancellationToken)) => WriteAsync(address, new[] { value }, token);
        private Task<OperationResult<T>> ExecuteAsync<T>(OperationResult<byte[]> command, bool write, Func<byte[], OperationResult<T>> parse, CancellationToken token)
        {
            if (!command.IsSuccess)
                return Task.FromResult(command.ConvertFailed<T>());
            return session.ExecuteAsync(async (tx, state) =>
            {
                var check = MpiSessionCodec.CheckAcknowledgement(await tx.ExchangeAsync(command.Content, new FixedLengthFrame(15)).ConfigureAwait(false));
                if (!check.IsSuccess)
                    return check.ConvertFailed<T>();
                byte[] response = await tx.ReceiveAsync(responseBoundary).ConfigureAwait(false);
                if (response.Length < (write ? 26 : 31))
                    return new OperationResult<T>("MPI 응답 길이가 부족합니다.");
                if (write)
                {
                    var result = MpiSessionCodec.ParseWrite(response);
                    if (!result.IsSuccess)
                        return result.ConvertFailed<T>();
                }
                else if (response[19] != 0)
                    return new OperationResult<T>("MPI 읽기 응답 오류: " + response[19]);
                await tx.SendAsync(MpiSessionCodec.BuildConfirmation(station, write)).ConfigureAwait(false);
                return parse(response);
            }, timeout, token);
        }
    }
}
