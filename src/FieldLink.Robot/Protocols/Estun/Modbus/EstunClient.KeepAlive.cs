using System;
using System.Threading;

namespace FieldLink.Robot.Protocols.Estun.Modbus;

public sealed partial class EstunClient
{
    private const ushort MaximumTransactionId = 0xFFFF;
    private const ushort InitialTransactionId = 0x0001;

    private void StartKeepAliveIfEnabled()
    {
        if (_options.KeepAliveInterval <= TimeSpan.Zero || _lifecycleGate.IsDisposed || Volatile.Read(ref _isOpen) == 0)
        {
            return;
        }

        var newTimer = new Timer(OnKeepAliveTick, null, _options.KeepAliveInitialDelay, _options.KeepAliveInterval);
        Timer? previousTimer = Interlocked.Exchange(ref _keepAliveTimer, newTimer);
        previousTimer?.Dispose();

        if (_lifecycleGate.IsDisposed || Volatile.Read(ref _isOpen) == 0)
        {
            StopKeepAlive();
        }
    }

    private void StopKeepAlive()
    {
        Timer? timer = Interlocked.Exchange(ref _keepAliveTimer, null);
        timer?.Dispose();
    }

    private void OnKeepAliveTick(object? state)
    {
        if (Interlocked.CompareExchange(ref _keepAliveInFlight, 1, 0) != 0)
        {
            return;
        }

        _ = PollKeepAliveSafeAsync();
    }

    private async System.Threading.Tasks.Task PollKeepAliveSafeAsync()
    {
        try
        {
            if (_lifecycleGate.IsDisposed || !IsOpen)
            {
                return;
            }

            using var timeoutCancellation = new CancellationTokenSource(_options.Timeout);
            await _transport.RunExclusiveAsync(
                session => ReadHoldingRegistersAsync(session, NextTransactionId(session.ConnectionId), HeartbeatRegisterAddress, 1),
                timeoutCancellation.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 백그라운드 Keep-Alive 폴링 실패로 프로세스가 종료되지 않도록 무시 (명시적 I/O에서 연결 이상 보고)
        }
        finally
        {
            Interlocked.Exchange(ref _keepAliveInFlight, 0);
        }
    }

    private ushort NextTransactionId(long connectionId)
    {
        lock (_transactionIdLock)
        {
            if (_lastConnectionId != connectionId)
            {
                _lastConnectionId = connectionId;
                _transactionId = 0;
            }

            _transactionId = (ushort)(_transactionId == MaximumTransactionId ? InitialTransactionId : _transactionId + 1);
            return _transactionId;
        }
    }
}
