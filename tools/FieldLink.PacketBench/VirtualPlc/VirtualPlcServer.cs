using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.PacketBench.Diagnostics;

namespace FieldLink.PacketBench.VirtualPlc;

/// <summary>127.0.0.1 에서 가상 PLC(서버 역할)로 동작하는 TCP 서버입니다.</summary>
internal sealed class VirtualPlcServer : IDisposable
{
    private readonly PacketLog _log;

    private TcpListener? _listener;
    private CancellationTokenSource? _cancellation;
    private IVirtualPlcResponder? _responder;
    private int _clientCount;

    public VirtualPlcServer(PacketLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>현재 실행 중인 프로토콜 응답기입니다. 중지 상태이면 null 입니다.</summary>
    public IVirtualPlcResponder? Responder => _responder;

    public VirtualPlcSettings Settings { get; } = new();

    public bool IsRunning => _listener is not null;

    public int Port { get; private set; }

    public int ClientCount => Volatile.Read(ref _clientCount);

    public void Start(int port, IVirtualPlcResponder responder)
    {
        if (IsRunning)
        {
            return;
        }

        _responder = responder ?? throw new ArgumentNullException(nameof(responder));

        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        _listener = listener;
        Port = port;
        _cancellation = new CancellationTokenSource();
        _ = AcceptLoopAsync(listener, responder, _cancellation.Token);
    }

    public void Stop()
    {
        CancellationTokenSource? cancellation = _cancellation;
        TcpListener? listener = _listener;

        _cancellation = null;
        _listener = null;
        _responder = null;

        cancellation?.Cancel();
        cancellation?.Dispose();
        listener?.Stop();
    }

    public void Dispose() => Stop();

    private async Task AcceptLoopAsync(TcpListener listener, IVirtualPlcResponder responder, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }

            _ = HandleClientAsync(client, responder, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, IVirtualPlcResponder responder, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _clientCount);

        try
        {
            client.NoDelay = true;
            using NetworkStream stream = client.GetStream();

            while (!cancellationToken.IsCancellationRequested)
            {
                byte[]? request = await responder.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                if (request is null)
                {
                    break;
                }

                _log.Record(PacketSource.VirtualPlc, PacketDirection.Received, request);

                if (Settings.SuppressResponse)
                {
                    continue;
                }

                if (Settings.ResponseDelayMilliseconds > 0)
                {
                    await Task.Delay(Settings.ResponseDelayMilliseconds, cancellationToken).ConfigureAwait(false);
                }

                byte[]? response = responder.Process(request, Settings);
                if (response is null)
                {
                    continue;
                }

                await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
                _log.Record(PacketSource.VirtualPlc, PacketDirection.Sent, response);
            }
        }
        catch (OperationCanceledException)
        {
            // 서버를 멈추면 진행 중인 연결도 함께 끝난다.
        }
        catch (IOException)
        {
            // 클라이언트가 연결을 끊었다.
        }
        catch (SocketException)
        {
            // 클라이언트가 연결을 끊었다.
        }
        finally
        {
            client.Dispose();
            Interlocked.Decrement(ref _clientCount);
        }
    }
}
