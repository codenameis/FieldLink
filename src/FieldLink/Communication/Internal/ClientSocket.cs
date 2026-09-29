using FieldLink.Communication.Diagnostics;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Communication.Internal
{
    // 클라이언트 소켓 하나의 소유권과 작업 순서를 관리한다. 메시지 형식과 장치 명령은 알지 못한다.
    internal sealed class ClientSocket : IDisposable
    {
        private readonly IPEndPoint remoteEndPoint;
        private readonly IPEndPoint localEndPoint;
        private readonly SocketType socketType;
        private readonly ProtocolType protocol;
        private readonly object sync = new object();
        private readonly SemaphoreSlim requests = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private CancellationTokenSource closeSignal = new CancellationTokenSource();
        private Socket socket;
        private IDisposable connectionMonitor;
        private bool disposed;
        private long generation;

        internal ClientSocket(object client, IPEndPoint remoteEndPoint, IPEndPoint localEndPoint,
            SocketType socketType, ProtocolType protocol)
        {
            this.remoteEndPoint = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
            this.localEndPoint = localEndPoint == null ? null : new IPEndPoint(localEndPoint.Address, localEndPoint.Port);
            this.socketType = socketType;
            this.protocol = protocol;
            Status = new ClientStateNotifier(client);
        }

        internal ClientStateNotifier Status { get; }
        internal long Generation { get { lock (sync) return generation; } }

        internal async Task<ClientOperation> BeginOpenAsync(TimeSpan timeout, CancellationToken token)
        {
            ClientOperation operation;
            lock (sync)
            {
                ThrowIfDisposed();
                if (socket != null)
                    throw new InvalidOperationException("Close the current client before opening it again.");
                operation = new ClientOperation(timeout, token, lifetime.Token, closeSignal.Token, remoteEndPoint);
            }
            await EnterAsync(operation).ConfigureAwait(false);
            try
            {
                operation.Stage = CommunicationStage.Connecting;
                lock (sync)
                {
                    VerifyOperationLifetime(operation);
                    if (socket != null)
                        throw new InvalidOperationException("Close the current client before opening it again.");
                    socket = new Socket(remoteEndPoint.AddressFamily, socketType, protocol);
                    operation.AttachSocket(socket);
                    Status.Change(ClientState.Opening);
                }
                if (localEndPoint != null)
                    operation.Socket.Bind(localEndPoint);
                return operation;
            }
            catch (Exception error)
            {
                try { operation.Fail(error); }
                finally { operation.Dispose(); }
                throw;
            }
        }

        internal async Task<ClientOperation> BeginRequestAsync(TimeSpan timeout, CancellationToken token)
        {
            Socket expectedSocket;
            ClientOperation operation;
            lock (sync)
            {
                ThrowIfDisposed();
                if (socket == null || Status.LastChange.State != ClientState.Open)
                    throw new InvalidOperationException("Complete OpenAsync before client communication.");
                expectedSocket = socket;
                // 상태 확인과 수명 등록 사이에 Close가 끼어들어 새 종료 구간을 잡지 않도록 한다.
                operation = new ClientOperation(timeout, token, lifetime.Token, closeSignal.Token, remoteEndPoint);
            }
            await EnterAsync(operation).ConfigureAwait(false);
            try
            {
                lock (sync)
                {
                    ThrowIfDisposed();
                    VerifyCurrentSocket(expectedSocket, operation);
                    operation.AttachSocket(expectedSocket);
                }
                return operation;
            }
            catch (Exception error)
            {
                try { operation.Fail(error); }
                finally { operation.Dispose(); }
                throw;
            }
        }

        private async Task EnterAsync(ClientOperation operation)
        {
            try
            {
                await requests.WaitAsync(operation.Token).ConfigureAwait(false);
                operation.OwnGate(this);
                operation.Token.ThrowIfCancellationRequested();
                lock (sync) VerifyOperationLifetime(operation);
            }
            catch (Exception error)
            {
                try { operation.Fail(error); }
                finally { operation.Dispose(); }
                throw;
            }
        }

        internal Task ConnectAsync(Socket socket) => socket.ConnectAsync(remoteEndPoint);

        internal void CompleteOpen(ClientOperation operation, IDisposable monitor)
        {
            lock (sync)
            {
                VerifyCurrentSocket(operation.Socket, operation);
                connectionMonitor = monitor;
                generation++;
                Status.Change(ClientState.Open);
            }
        }

        internal void VerifyCurrentSocket(Socket expectedSocket, ClientOperation operation)
        {
            lock (sync)
            {
                VerifyOperationLifetime(operation);
                if (!ReferenceEquals(socket, expectedSocket))
                    throw operation.Failure(CommunicationFailure.ConnectionClosed, "The client socket was closed or replaced.");
            }
        }

        internal void Invalidate(Socket failedSocket, Exception error)
        {
            if (failedSocket == null)
                return;
            lock (sync)
            {
                failedSocket.Dispose();
                if (!ReferenceEquals(socket, failedSocket))
                    return;
                ReleaseSocket();
                bool peerClosed = (error as CommunicationException)?.Failure == CommunicationFailure.ConnectionClosed;
                Status.Change(peerClosed ? ClientState.Closed : ClientState.Faulted, error);
            }
        }

        internal void InspectIdle(Socket expectedSocket, Func<Exception> inspect)
        {
            if (!requests.Wait(0))
                return;
            try
            {
                lock (sync)
                {
                    if (disposed || !ReferenceEquals(socket, expectedSocket) || Status.LastChange.State != ClientState.Open)
                        return;
                    Exception failure = inspect();
                    if (failure != null)
                        Invalidate(expectedSocket, failure);
                }
            }
            finally { requests.Release(); }
        }

        internal void ReleaseRequest() => requests.Release();

        internal void Close(Socket expectedSocket = null)
        {
            CancellationTokenSource closed;
            lock (sync)
            {
                if (disposed)
                    return;
                if (expectedSocket != null && !ReferenceEquals(socket, expectedSocket))
                    return;
                closed = closeSignal;
                closeSignal = new CancellationTokenSource();
                ReleaseSocket();
                Status.Change(ClientState.Closed);
            }

            // Cancel은 콜백을 동기 실행한다. 잠금 밖에서 이전 구간의 작업만 취소한다.
            try { closed.Cancel(); }
            finally { closed.Dispose(); }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                Status.Change(ClientState.Disposed);
            }
            try { lifetime.Cancel(); }
            finally
            {
                // 드라이버가 등록한 취소 콜백이 예외를 던져도 소켓·감시 자원은 정리한다.
                lock (sync)
                {
                    ReleaseSocket();
                    closeSignal.Dispose();
                }
            }
            // 대기·진행 중 작업이 취소 등록과 게이트를 정리할 때까지 이 두 관리 객체는 살아 있어야 한다.
        }

        private void ReleaseSocket()
        {
            connectionMonitor?.Dispose();
            connectionMonitor = null;
            socket?.Dispose();
            socket = null;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException("client");
        }

        private void VerifyOperationLifetime(ClientOperation operation)
        {
            if (disposed)
                throw operation.Failure(CommunicationFailure.Disposed, "The client was disposed.");
            // Cancel이 실행되기 전이라도 Close 이전에 대기하던 작업은 새 연결을 만들 수 없다.
            if (operation.ConnectionCancellation != closeSignal.Token)
                throw operation.Failure(CommunicationFailure.ConnectionClosed, "The client was closed after this operation started.");
        }
    }
}
