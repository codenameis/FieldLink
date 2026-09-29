using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Diagnostics;
using FieldLink.PlcDrivers.Common;

namespace FieldLink.PlcDrivers.Melsec.Clients
{
    /// <summary>MC 3E Binary TCP의 연결·자료형 변환·분할 읽기/쓰기를 묶는 클라이언트입니다.</summary>
    /// <remarks>생성 후 OpenAsync를 명시적으로 호출합니다. 자동 재접속·명령 재전송은 하지 않습니다.</remarks>
    public sealed partial class MelsecMcClient : IDisposable
    {
        private readonly ITcpClient transport;
        private readonly Func<string, ushort, bool, OperationResult<McDeviceAddress>> parseAddress;
        private readonly bool ownsTransport;
        private readonly TimeSpan timeout;
        private readonly McFrameOptions route;
        private readonly ProtocolValueConverter converter;
        private readonly Encoding stringEncoding;
        private readonly int maxWords;
        private readonly int maxBits;
        private readonly MelsecMc3EBinaryFrame boundary = new MelsecMc3EBinaryFrame();
        private int disposed;

        /// <summary>외부 전송을 빌려 사용합니다. Open/Close는 위임하지만 Dispose는 외부 전송을 해제하지 않습니다.</summary>
        public MelsecMcClient(ITcpClient transport, McClientOptions options = null) : this(transport, options, false) { }

        internal MelsecMcClient(ITcpClient transport, McClientOptions options, bool ownsTransport,
            Func<string, ushort, bool, OperationResult<McDeviceAddress>> parseAddress = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.ownsTransport = ownsTransport;
            this.parseAddress = parseAddress ?? McDeviceAddress.ParseMelsecFrom;
            options = options ?? new McClientOptions();
            if (options.Timeout.TotalMilliseconds < 1 || options.Timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(options), "제한 시간은 1~Int32.MaxValue 밀리초입니다.");
            if (options.MaxWordsPerRequest < 1 || options.MaxWordsPerRequest > 950 ||
                options.MaxBitsPerRequest < 1 || options.MaxBitsPerRequest > 950)
                throw new ArgumentOutOfRangeException(nameof(options), "블록 크기는 1~950입니다.");
            McFrameOptions source = options.Route ?? throw new ArgumentException("MC 경로가 필요합니다.", nameof(options));
            route = new McFrameOptions { NetworkNumber = source.NetworkNumber, PLCNumber = source.PLCNumber,
                TargetIOStation = source.TargetIOStation, NetworkStationNumber = source.NetworkStationNumber };
            timeout = options.Timeout;
            maxWords = options.MaxWordsPerRequest;
            maxBits = options.MaxBitsPerRequest;
            converter = new ProtocolValueConverter(options.ByteOrder, options.SwapStringBytes);
            stringEncoding = StrictEncoding(options.StringEncoding ?? throw new ArgumentException("문자열 인코딩이 필요합니다.", nameof(options)));
        }

        /// <summary>조회 시점의 전송 상태입니다. 원격 장치의 생존 또는 다음 요청 성공을 보장하지 않습니다.</summary>
        public ClientState State => Volatile.Read(ref disposed) != 0 ? ClientState.Disposed : transport.State;

        /// <summary>TCP 연결을 엽니다. 접속 실패는 진단 정보가 담긴 실패 결과, 호출자 취소는 예외로 전달합니다.</summary>
        public async Task<OperationResult> OpenAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CheckLifetime(cancellationToken);
            try
            {
                await transport.OpenAsync(timeout, cancellationToken).ConfigureAwait(false);
                return OperationResult.CreateSuccessResult();
            }
            catch (CommunicationException error)
            {
                return Failure(error, new OperationFailureDetails(null, "Open", null, 0, 0, error), cancellationToken);
            }
        }

        /// <summary>전송을 닫습니다. MC에는 별도의 정상 종료 명령이 없으며 이후 다시 Open할 수 있습니다.</summary>
        public void Close()
        {
            CheckLifetime(CancellationToken.None);
            transport.Close();
        }

        /// <summary>클라이언트를 해제하고, 간편 생성으로 소유한 전송만 해제합니다.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && ownsTransport)
                transport.Dispose();
        }

        private void CheckLifetime(CancellationToken token)
        {
            if (Volatile.Read(ref disposed) != 0 || transport.State == ClientState.Disposed)
                throw new ObjectDisposedException(nameof(MelsecMcClient));
            token.ThrowIfCancellationRequested();
        }

        private void CheckRequest(CancellationToken token)
        {
            CheckLifetime(token);
            if (transport.State != ClientState.Open)
                throw new InvalidOperationException("요청 전에 OpenAsync를 완료하세요.");
        }

        private static Encoding StrictEncoding(Encoding encoding)
        {
            var copy = (Encoding)encoding.Clone();
            copy.EncoderFallback = EncoderFallback.ExceptionFallback;
            copy.DecoderFallback = DecoderFallback.ExceptionFallback;
            return copy;
        }

        private static OperationResult<byte[]> Failure(CommunicationException error, OperationFailureDetails details, CancellationToken token)
        {
            if (error.Failure == CommunicationFailure.Cancelled)
                throw new PlcOperationCanceledException(details, token);
            return new OperationResult<byte[]>(-1000 - (int)error.Failure, error.Message) { FailureDetails = details };
        }

        private static OperationResult<byte[]> Invalid(string message) => new OperationResult<byte[]>(-1, message);
    }
}
