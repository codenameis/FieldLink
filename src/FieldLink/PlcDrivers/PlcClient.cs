using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers.Common;
using FieldLink.PlcDrivers.Common.Clients;

namespace FieldLink.PlcDrivers
{
    /// <summary>제조사별 드라이버를 조합하여 연결과 자료형별 읽기/쓰기를 제공하는 간편 TCP 클라이언트입니다.</summary>
    /// <remarks>Plc의 제조사별 팩터리로 생성합니다. 전송을 소유하며 자동 연결·재전송은 하지 않습니다.</remarks>
    public sealed partial class PlcClient : IDisposable
    {
        private readonly ITcpClient transport;
        private readonly IPlcDriver driver;
        private readonly TimeSpan timeout;
        private readonly ProtocolValueConverter converter;
        private readonly Encoding stringEncoding;
        private int disposed;
        private int ready;
        private int lifecycle;

        internal PlcClient(ITcpClient transport, IPlcDriver driver, PlcClientOptions options, ByteOrder defaultOrder)
        {
            this.transport = transport;
            this.driver = driver;
            options = options ?? new PlcClientOptions();
            if (options.Timeout.TotalMilliseconds < 1 || options.Timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(options));
            timeout = options.Timeout;
            converter = new ProtocolValueConverter(options.ByteOrder ?? defaultOrder);
            stringEncoding = StrictEncoding(options.StringEncoding ?? throw new ArgumentException("인코딩이 필요합니다.", nameof(options)));
        }

        /// <summary>조회 시점의 전송 상태입니다. 프로토콜 초기화 완료나 원격 장치의 생존을 보장하지 않습니다.</summary>
        public ClientState State => Volatile.Read(ref disposed) != 0 ? ClientState.Disposed : transport.State;

        /// <summary>TCP 연결과 제조사별 협상을 완료합니다. 예상 가능한 실패는 결과로 반환합니다.</summary>
        public async Task<OperationResult> OpenAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckLifetime(cancellationToken);
            EnterLifecycle();
            var watch = Stopwatch.StartNew();
            try
            {
                Volatile.Write(ref ready, 0);
                await transport.OpenAsync(timeout, cancellationToken).ConfigureAwait(false);
                await driver.InitializeAsync(Remaining(watch), cancellationToken).ConfigureAwait(false);
                CheckLifetime(cancellationToken);
                if (transport.State != ClientState.Open)
                    throw new InvalidOperationException("초기화 중 연결이 종료되었습니다.");
                Volatile.Write(ref ready, 1);
                return OperationResult.CreateSuccessResult();
            }
            catch (CommunicationException error) { return Failure(error, null, cancellationToken); }
            catch (PlcProtocolException error) { return ProtocolFailure(error, null); }
            catch (InvalidDataException error) { return InvalidFrame(error, null); }
            catch (TimeoutException error)
            {
                transport.Close();
                return new OperationResult(-1000 - (int)CommunicationFailure.Timeout, error.Message)
                { FailureDetails = new OperationFailureDetails(null, "Open", null, 0, 0, error) };
            }
            finally { Volatile.Write(ref lifecycle, 0); }
        }

        // timeout을 새로 시작하지 않고 연결에 사용한 시간을 협상 제한 시간에서 뺀다.
        private TimeSpan Remaining(Stopwatch watch)
        {
            TimeSpan remaining = timeout - watch.Elapsed;
            if (remaining.TotalMilliseconds < 1)
                throw new TimeoutException("PLC 연결/협상 제한 시간이 지났습니다.");
            return remaining;
        }

        /// <summary>프로토콜의 정상 해제 절차를 수행하고 TCP 연결을 닫습니다. 실패한 명령은 재전송하지 않습니다.</summary>
        public async Task<OperationResult> CloseAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckLifetime(cancellationToken);
            EnterLifecycle();
            try
            {
                if (transport.State == ClientState.Open && Volatile.Read(ref ready) != 0)
                    await driver.ShutdownAsync(timeout, cancellationToken).ConfigureAwait(false);
                return OperationResult.CreateSuccessResult();
            }
            catch (CommunicationException error) { return Failure(error, null, cancellationToken); }
            catch (PlcProtocolException error) { return ProtocolFailure(error, null); }
            catch (InvalidDataException error) { return InvalidFrame(error, null); }
            finally
            {
                Volatile.Write(ref ready, 0);
                transport.Close();
                Volatile.Write(ref lifecycle, 0);
            }
        }

        /// <summary>정상 해제 메시지를 기다리지 않고 연결을 닫아 진행 중인 작업을 중단합니다.</summary>
        public void Close()
        {
            CheckLifetime(CancellationToken.None);
            Volatile.Write(ref ready, 0);
            transport.Close();
        }

        /// <summary>소유한 TCP 전송을 해제합니다. 비동기 정상 해제가 필요하면 먼저 CloseAsync를 호출하세요.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                Volatile.Write(ref ready, 0);
                transport.Dispose();
            }
        }

        private void EnterLifecycle()
        {
            if (Interlocked.CompareExchange(ref lifecycle, 1, 0) != 0)
                throw new InvalidOperationException("다른 연결/해제 작업이 진행 중입니다.");
        }
        private void CheckLifetime(CancellationToken token)
        {
            if (Volatile.Read(ref disposed) != 0)
                throw new ObjectDisposedException(nameof(PlcClient));
            token.ThrowIfCancellationRequested();
        }
        private void CheckRequest(CancellationToken token)
        {
            CheckLifetime(token);
            if (Volatile.Read(ref ready) == 0 || transport.State != ClientState.Open)
                throw new InvalidOperationException("요청 전에 OpenAsync를 완료하세요.");
        }

        private async Task<OperationResult<byte[]>> ExecuteAsync(PlcTransferRequest request, CancellationToken token)
        {
            CheckRequest(token);
            try { return await driver.TransferAsync(request, timeout, token).ConfigureAwait(false); }
            catch (CommunicationException error) { return Failure(error, request, token); }
            catch (PlcProtocolException error) { return ProtocolFailure(error, request); }
            catch (InvalidDataException error) { return InvalidFrame(error, request); }
            catch (OperationCanceledException error)
            {
                throw new PlcOperationCanceledException(request.Details(error), token);
            }
        }

        private static OperationResult<byte[]> Failure(CommunicationException error, PlcTransferRequest request, CancellationToken token)
        {
            var details = request == null ? new OperationFailureDetails(null, "Connection", null, 0, 0, error)
                : request.Details(error, error.BytesSent != 0);
            if (error.Failure == CommunicationFailure.Cancelled)
                throw new PlcOperationCanceledException(details, token);
            return new OperationResult<byte[]>(-1000 - (int)error.Failure, error.Message) { FailureDetails = details };
        }
        private static OperationResult<byte[]> ProtocolFailure(PlcProtocolException error, PlcTransferRequest request) =>
            new OperationResult<byte[]>(error.Code, error.Message) { FailureDetails = request?.Details(error) ?? new OperationFailureDetails(null, "Connection", null, 0, 0, error) };
        private static OperationResult<byte[]> InvalidFrame(InvalidDataException error, PlcTransferRequest request) =>
            new OperationResult<byte[]>(-1000 - (int)CommunicationFailure.InvalidFrame, error.Message) { FailureDetails = request?.Details(error) ?? new OperationFailureDetails(null, "Connection", null, 0, 0, error) };

        private static OperationResult<T> WithContent<T>(OperationResult status, T content) =>
            new OperationResult<T> { IsSuccess = true, Content = content, ErrorCode = status.ErrorCode, Message = status.Message };

        private static Encoding StrictEncoding(Encoding encoding)
        {
            var copy = (Encoding)encoding.Clone();
            copy.EncoderFallback = EncoderFallback.ExceptionFallback;
            copy.DecoderFallback = DecoderFallback.ExceptionFallback;
            return copy;
        }
        private async Task<OperationResult<T[]>> ReadValuesAsync<T>(string address, int count, PlcValueType type,
            Func<byte[], int, int, T[]> decode, CancellationToken token)
        {
            var result = await ExecuteAsync(new PlcTransferRequest(address, type, count), token).ConfigureAwait(false);
            return result.IsSuccess ? WithContent(result, decode(result.Content, 0, count)) : result.ConvertFailed<T[]>();
        }
        private static async Task<OperationResult<T>> FirstAsync<T>(Task<OperationResult<T[]>> pending)
        {
            var result = await pending.ConfigureAwait(false);
            return result.IsSuccess ? WithContent(result, result.Content[0]) : result.ConvertFailed<T>();
        }
        private async Task<OperationResult> WriteValueAsync(string address, int count, PlcValueType type, Func<byte[]> encode, CancellationToken token) =>
            await ExecuteAsync(new PlcTransferRequest(address, type, count, encode), token).ConfigureAwait(false);
        private Task<OperationResult> WriteValuesAsync<T>(string address, T[] values, PlcValueType type, Func<T[], byte[]> encode, CancellationToken token)
        {
            CheckRequest(token);
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            return WriteValueAsync(address, values.Length, type, () => encode(values), token);
        }
    }
}
