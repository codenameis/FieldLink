using FieldLink.Communication.Diagnostics;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication.Internal;
using FieldLink.Communication.Framing;

namespace FieldLink.Communication.Tcp
{
    /// <summary>단일 장치에 연결하여 메시지를 교환하는 TCP 클라이언트입니다.</summary>
    public sealed class TcpClient : ITcpClient
    {
        private readonly ClientSocket connection;
        private readonly IFrameBoundary responseBoundary;
        private readonly int maximumFrameLength;
        private readonly TimeSpan timeout;
        private readonly TimeSpan disconnectCheckInterval;
        private readonly string remoteAddress;
        private TcpFrameBuffer receiveBuffer;

        /// <summary>응답 경계와 기본 제한 시간을 지정합니다. 기본 제한 시간은 3초, 유휴 종료 확인 간격은 250ms입니다.</summary>
        public TcpClient(IPEndPoint remoteEndPoint, IFrameBoundary responseBoundary,
            int maximumFrameLength = 1024 * 1024, IPEndPoint localEndPoint = null,
            TimeSpan? timeout = null, TimeSpan? disconnectCheckInterval = null)
        {
            if (remoteEndPoint == null)
                throw new ArgumentNullException(nameof(remoteEndPoint));
            if (responseBoundary == null)
                throw new ArgumentNullException(nameof(responseBoundary));
            if (maximumFrameLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumFrameLength));
            this.timeout = timeout ?? TimeSpan.FromSeconds(3);
            ClientOperation.ValidateTimeout(this.timeout);
            this.disconnectCheckInterval = disconnectCheckInterval ?? TimeSpan.FromMilliseconds(250);
            if (this.disconnectCheckInterval != Timeout.InfiniteTimeSpan)
                ClientOperation.ValidateTimeout(this.disconnectCheckInterval);
            this.responseBoundary = responseBoundary;
            this.maximumFrameLength = maximumFrameLength;
            remoteAddress = remoteEndPoint.ToString();
            connection = new ClientSocket(this, remoteEndPoint, localEndPoint, SocketType.Stream, ProtocolType.Tcp);
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
        public bool IsConnected => State == ClientState.Open;
        /// <inheritdoc />
        public long ConnectionGeneration => connection.Generation;

        /// <inheritdoc />
        public Task OpenAsync(CancellationToken cancellationToken = default(CancellationToken))
            => OpenAsync(timeout, cancellationToken);

        /// <inheritdoc />
        public async Task OpenAsync(TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (ClientOperation operation = await connection.BeginOpenAsync(timeout, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    operation.Socket.NoDelay = true;
                    operation.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    receiveBuffer = new TcpFrameBuffer(maximumFrameLength);
                    await connection.ConnectAsync(operation.Socket).ConfigureAwait(false);
                    operation.CompleteOpen(CreateDisconnectMonitor(operation.Socket, receiveBuffer));
                }
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

        /// <inheritdoc />
        public Task SendAsync(byte[] message, TimeSpan timeout, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            return SendRequestAsync(message, timeout, cancellationToken);
        }

        private async Task SendRequestAsync(byte[] request, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using (ClientOperation operation = await connection.BeginRequestAsync(timeout, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var exchange = new TcpExchange(operation.Socket, receiveBuffer, operation);
                    await exchange.SendAsync(request).ConfigureAwait(false);
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
            => ExchangeAsync(request, responseBoundary, timeout, classifyResponse, cancellationToken);

        /// <inheritdoc />
        public Task<byte[]> ExchangeAsync(byte[] request, TimeSpan timeout,
            Func<byte[], ResponseDisposition> classifyResponse = null,
            CancellationToken cancellationToken = default(CancellationToken))
            => ExchangeAsync(request, responseBoundary, timeout, classifyResponse, cancellationToken);

        /// <inheritdoc />
        public Task<byte[]> ExchangeAsync(byte[] request, IFrameBoundary boundary, TimeSpan timeout,
            Func<byte[], ResponseDisposition> classifyResponse = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (boundary == null)
                throw new ArgumentNullException(nameof(boundary));
            return ExchangeRequestAsync(request, boundary, timeout, classifyResponse, cancellationToken);
        }

        private async Task<byte[]> ExchangeRequestAsync(byte[] request, IFrameBoundary boundary, TimeSpan timeout,
            Func<byte[], ResponseDisposition> classifyResponse, CancellationToken cancellationToken)
        {
            using (ClientOperation operation = await connection.BeginRequestAsync(timeout, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var exchange = new TcpExchange(operation.Socket, receiveBuffer, operation);
                    byte[] response = await exchange.ExchangeAsync(request, boundary, classifyResponse).ConfigureAwait(false);
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
        public Task<T> ExecuteTransactionAsync<T>(Func<ITcpTransaction, Task<T>> action,
            CancellationToken cancellationToken = default(CancellationToken))
            => ExecuteTransactionAsync(action, timeout, cancellationToken);

        /// <inheritdoc />
        public Task<T> ExecuteTransactionAsync<T>(Func<ITcpTransaction, Task<T>> action, TimeSpan timeout,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            return RunTransactionAsync(action, timeout, cancellationToken);
        }

        private async Task<T> RunTransactionAsync<T>(Func<ITcpTransaction, Task<T>> action, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using (ClientOperation operation = await connection.BeginRequestAsync(timeout, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var exchange = new TcpExchange(operation.Socket, receiveBuffer, operation);
                    var transaction = new TcpTransaction(operation.Socket, exchange, responseBoundary,
                        connection.Generation, operation.Token);
                    T response = await transaction.ExecuteAsync(action).ConfigureAwait(false);
                    operation.Complete();
                    if (transaction.CloseRequested)
                        connection.Close(operation.Socket);
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

        private Timer CreateDisconnectMonitor(Socket socket, TcpFrameBuffer buffer)
        {
            if (disconnectCheckInterval == Timeout.InfiniteTimeSpan)
                return null;
            Func<Exception> inspect = () => InspectDisconnect(socket, buffer);
            return new Timer(_ => connection.InspectIdle(socket, inspect), null,
                disconnectCheckInterval, disconnectCheckInterval);
        }

        private Exception InspectDisconnect(Socket socket, TcpFrameBuffer buffer)
        {
            // FIN 앞의 메시지는 소비하지 않는다. 요청과 경합하지 않는 유휴 구간에서만 호출된다.
            if (buffer.HasBufferedData)
                return null;
            try
            {
                if (!socket.Poll(0, SelectMode.SelectRead) || socket.Available != 0)
                    return null;
                return new CommunicationException(CommunicationFailure.ConnectionClosed, CommunicationStage.Monitoring,
                    remoteAddress, 0, 0, new IOException("Peer closed the idle TCP connection."));
            }
            catch (SocketException cause)
            {
                return new CommunicationException(CommunicationFailure.SocketError, CommunicationStage.Monitoring,
                    remoteAddress, 0, 0, cause);
            }
        }
    }
}
