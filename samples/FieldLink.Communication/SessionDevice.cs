using System;
using System.Threading;
using System.Threading.Tasks;
using FieldLink.Communication;
using FieldLink.Communication.Diagnostics;
using FieldLink.Communication.Framing;

namespace FieldLink.Samples;

// 가상 프로토콜 예제다. 제품 라이브러리에 포함되지 않는다.
// 응답은 [오류 코드, 값/세션] 2바이트이며 오류 코드 0은 성공이다.
// 이 드라이버를 사용하는 동안 client의 열기·요청·종료는 모두 드라이버를 통해 수행한다.
public sealed class SessionDevice
{
    private const byte InitializeCommand = 0x10;
    private const byte ReadCommand = 0x20;
    private const byte WriteCommand = 0x21;
    private const byte DisconnectCommand = 0x30;
    private static readonly IFrameBoundary ReplyFrame = new FixedLengthFrame(2);
    private readonly ITcpClient client;
    // 클라이언트의 교환 잠금과 별도로, 장치 오류 해석과 정상 종료까지 요청 진입을 조율한다.
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly object sync = new();
    private DriverState state;
    private long initializedGeneration;
    private byte session;

    private enum DriverState { Closed, Opening, Active, Closing }

    public SessionDevice(ITcpClient client)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        state = client.State == ClientState.Open ? DriverState.Active : DriverState.Closed;
    }

    // 소켓을 명시적으로 연다. 장치 초기화는 첫 요청의 독점 구간에서 실행한다.
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            if (state == DriverState.Opening || state == DriverState.Closing || client.State == ClientState.Open)
                throw new InvalidOperationException("The device is already open or changing its connection.");
            state = DriverState.Opening;
        }

        bool entered = false;
        try
        {
            await operations.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            await client.OpenAsync(cancellationToken).ConfigureAwait(false);
            initializedGeneration = 0;
            lock (sync)
                state = DriverState.Active;
        }
        catch
        {
            lock (sync)
                state = DriverState.Closed;
            throw;
        }
        finally
        {
            if (entered)
                operations.Release();
        }
    }

    public Task<byte> ReadAsync(CancellationToken cancellationToken = default)
        => ExecuteCommandAsync(ReadCommand, null, cancellationToken);

    public async Task WriteAsync(byte value, CancellationToken cancellationToken = default)
        => await ExecuteCommandAsync(WriteCommand, value, cancellationToken).ConfigureAwait(false);

    private async Task<byte> ExecuteCommandAsync(byte command, byte? value, CancellationToken cancellationToken)
    {
        long requestedGeneration = RequireActiveConnection();
        await operations.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (RequireActiveConnection() != requestedGeneration)
                throw new InvalidOperationException("The connection changed while this command was queued.");
            byte[] response = await client.ExecuteTransactionAsync(async transaction =>
            {
                if (initializedGeneration != transaction.ConnectionGeneration)
                {
                    byte[] initialization = await transaction.ExchangeAsync(
                        new byte[] { InitializeCommand }, ReplyFrame).ConfigureAwait(false);
                    // 초기화 거절은 세션을 사용할 수 없는 실패다. 예외로 트랜잭션과 연결을 종료한다.
                    ThrowIfRejected(initialization);
                    session = initialization[1];
                    initializedGeneration = transaction.ConnectionGeneration;
                }
                byte[] request = value.HasValue
                    ? new byte[] { command, session, value.Value }
                    : new byte[] { command, session };
                return await transaction.ExchangeAsync(request, ReplyFrame).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            // 정상 형식의 명령 거절은 통신 성공 뒤에 전달한다. 건강한 연결을 폐기하지 않는다.
            ThrowIfRejected(response);
            return response[1];
        }
        finally { operations.Release(); }
    }

    // 정상 종료: 새 요청을 막고 진행 중 요청이 끝난 뒤 장치 세션을 해제한다.
    // 종료 대기/교환이 취소되거나 실패하면 finally에서 소켓을 즉시 닫는다.
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            if (state == DriverState.Closed)
                return;
            if (state != DriverState.Active)
                throw new InvalidOperationException("The device is already changing its connection.");
            state = DriverState.Closing;
        }

        bool entered = false;
        try
        {
            await operations.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            if (client.State == ClientState.Open && initializedGeneration == client.ConnectionGeneration)
            {
                byte[] response = await client.ExchangeAsync(new byte[] { DisconnectCommand, session },
                    ReplyFrame, TimeSpan.FromSeconds(2), cancellationToken: cancellationToken).ConfigureAwait(false);
                ThrowIfRejected(response);
            }
        }
        finally
        {
            // 대기 중 취소됐다면 진행 중 명령도 종료한다. 새 Open은 그 명령의 게이트 반환을 기다린다.
            try { client.Close(); }
            finally
            {
                lock (sync) state = DriverState.Closed;
                if (entered)
                    operations.Release();
            }
        }
    }

    private long RequireActiveConnection()
    {
        lock (sync)
        {
            if (state != DriverState.Active)
                throw new InvalidOperationException("Open the device before issuing commands; closing devices reject commands.");
            return client.ConnectionGeneration;
        }
    }

    private static void ThrowIfRejected(byte[] response)
    {
        if (response[0] != 0)
            throw new DeviceCommandException(response[0]);
    }
}

// 실제 드라이버에서는 장치 규격의 오류 코드와 설명을 보존한다.
public sealed class DeviceCommandException : Exception
{
    public DeviceCommandException(byte errorCode) : base($"Device rejected the command: 0x{errorCode:X2}.")
    {
        ErrorCode = errorCode;
    }

    public byte ErrorCode { get; }
}
