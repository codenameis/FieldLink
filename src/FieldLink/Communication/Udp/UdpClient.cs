using FieldLink.Communication.Diagnostics;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication.Internal;

namespace FieldLink.Communication.Udp
{
    /// <summary>하나의 장치와 데이터그램을 교환하는 UDP 클라이언트입니다. 자동 재전송하지 않습니다.</summary>
    public sealed class UdpClient : IUdpClient
    {
        private const int MaximumPayloadLength = 65507;
        private const int DatagramReceiveBufferLength = 65535;
        private readonly ClientSocket connection;
        private readonly IPEndPoint remoteEndPoint;
        private readonly int maximumDatagramLength;
        private readonly TimeSpan timeout;
        private byte[] receiveBuffer;

        /// <summary>상대 주소, 데이터그램 최대 크기, 로컬 주소와 기본 제한 시간을 지정합니다.</summary>
        public UdpClient(IPEndPoint remoteEndPoint, int maximumDatagramLength = MaximumPayloadLength,
            IPEndPoint localEndPoint = null, TimeSpan? timeout = null)
        {
            if (remoteEndPoint == null)
                throw new ArgumentNullException(nameof(remoteEndPoint));
            if (maximumDatagramLength <= 0 || maximumDatagramLength > MaximumPayloadLength)
                throw new ArgumentOutOfRangeException(nameof(maximumDatagramLength));
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            ClientOperation.ValidateTimeout(this.timeout);
            this.remoteEndPoint = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
            this.maximumDatagramLength = maximumDatagramLength;
            IPEndPoint bindAddress = localEndPoint ?? new IPEndPoint(
                remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
            connection = new ClientSocket(this, remoteEndPoint, bindAddress, SocketType.Dgram, ProtocolType.Udp);
        }

        /// <inheritdoc />
        public ClientState State => connection.Status.LastChange.State;
        /// <inheritdoc />
        public ClientStateChangedEventArgs LastStateChange => connection.Status.LastChange;
        /// <inheritdoc />
        public Exception LastNotificationError => connection.Status.LastNotificationError;
        /// <inheritdoc />
        public event EventHandler<ClientStateChangedEventArgs> StateChanged
        {
            add { connection.Status.Changed += value; }
            remove { connection.Status.Changed -= value; }
        }

        /// <inheritdoc />
        public Task OpenAsync(CancellationToken cancellationToken = default(CancellationToken))
            => OpenAsync(timeout, cancellationToken);

        /// <summary>UDP 소켓을 바인딩합니다. 원격 장치와의 연결 수립을 의미하지 않습니다.</summary>
        public async Task OpenAsync(TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (ClientOperation operation = await connection.BeginOpenAsync(timeout, cancellationToken).ConfigureAwait(false))
            {
                try { operation.CompleteOpen(); }
                catch (Exception error)
                {
                    operation.Fail(error);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public Task SendAsync(byte[] message, CancellationToken cancellationToken = default(CancellationToken))
            => SendAsync(message, timeout, cancellationToken);

        /// <summary>지정한 제한 시간으로 데이터그램 하나를 보냅니다. 장치 처리 완료를 보장하지 않습니다.</summary>
        public Task SendAsync(byte[] message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateDatagram(message, nameof(message));
            return SendRequestAsync(message, timeout, cancellationToken);
        }

        private async Task SendRequestAsync(byte[] message, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using (ClientOperation operation = await connection.BeginRequestAsync(timeout, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await SendDatagramAsync(message, operation).ConfigureAwait(false);
                    operation.Complete();
                }
                catch (Exception error)
                {
                    operation.Fail(error);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public Task<byte[]> ExchangeAsync(byte[] request, Func<byte[], ResponseDisposition> classifyResponse = null,
            CancellationToken cancellationToken = default(CancellationToken))
            => ExchangeAsync(request, timeout, classifyResponse, cancellationToken);

        /// <summary>지정한 제한 시간 안에 같은 상대 주소·포트의 응답을 받습니다.</summary>
        public Task<byte[]> ExchangeAsync(byte[] request, TimeSpan timeout,
            Func<byte[], ResponseDisposition> classifyResponse = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateDatagram(request, nameof(request));
            return ExchangeRequestAsync(request, timeout, classifyResponse, cancellationToken);
        }

        private async Task<byte[]> ExchangeRequestAsync(byte[] request, TimeSpan timeout,
            Func<byte[], ResponseDisposition> classifyResponse, CancellationToken cancellationToken)
        {
            using (ClientOperation operation = await connection.BeginRequestAsync(timeout, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await SendDatagramAsync(request, operation).ConfigureAwait(false);
                    byte[] response = await ReceiveResponseAsync(classifyResponse, operation).ConfigureAwait(false);
                    operation.Complete();
                    return response;
                }
                catch (Exception error)
                {
                    operation.Fail(error);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public void Close() => connection.Close();

        /// <inheritdoc />
        public void Dispose() => connection.Dispose();

        private async Task SendDatagramAsync(byte[] message, ClientOperation operation)
        {
            operation.Token.ThrowIfCancellationRequested();
            operation.Stage = CommunicationStage.Sending;
            int sent = await operation.Socket.SendToAsync(new ArraySegment<byte>(message),
                SocketFlags.None, remoteEndPoint).ConfigureAwait(false);
            operation.BytesSent += sent;
            if (sent != message.Length)
                throw operation.Failure(CommunicationFailure.SocketError, $"Datagram send accepted {sent} of {message.Length} bytes.");
        }

        private async Task<byte[]> ReceiveResponseAsync(Func<byte[], ResponseDisposition> classifyResponse,
            ClientOperation operation)
        {
            // 큰 외부 패킷도 먼저 상대 주소로 걸러내야 하므로 설정한 최대 길이보다 넉넉하게 수신한다.
            if (receiveBuffer == null)
                receiveBuffer = new byte[DatagramReceiveBufferLength];
            EndPoint sender = new IPEndPoint(remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6
                ? IPAddress.IPv6Any : IPAddress.Any, 0);
            while (true)
            {
                operation.Token.ThrowIfCancellationRequested();
                operation.Stage = CommunicationStage.Receiving;
                SocketReceiveMessageFromResult received = await operation.Socket.ReceiveMessageFromAsync(
                    new ArraySegment<byte>(receiveBuffer), SocketFlags.None, sender).ConfigureAwait(false);
                operation.BytesReceived += received.ReceivedBytes;
                if (!remoteEndPoint.Equals(received.RemoteEndPoint))
                    continue;

                operation.Stage = CommunicationStage.Validating;
                if ((received.SocketFlags & SocketFlags.Truncated) != 0 || received.ReceivedBytes > maximumDatagramLength)
                    throw operation.Failure(CommunicationFailure.MessageTooLarge, "Received datagram exceeds the configured limit.");

                byte[] response = new byte[received.ReceivedBytes];
                Buffer.BlockCopy(receiveBuffer, 0, response, 0, response.Length);
                ResponseDisposition disposition = classifyResponse?.Invoke(response) ?? ResponseDisposition.Accept;
                if (disposition == ResponseDisposition.Accept)
                    return response;
                if (disposition != ResponseDisposition.Ignore)
                    throw operation.Failure(CommunicationFailure.ResponseRejected, "Response classifier rejected the datagram.");
            }
        }

        private void ValidateDatagram(byte[] message, string parameterName)
        {
            if (message == null)
                throw new ArgumentNullException(parameterName);
            if (message.Length > maximumDatagramLength)
                throw new ArgumentOutOfRangeException(parameterName, "Datagram exceeds the configured payload limit.");
        }
    }
}
