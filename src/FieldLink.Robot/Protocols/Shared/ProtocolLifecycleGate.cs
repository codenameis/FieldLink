using System;
using System.Threading;
using System.Threading.Tasks;

namespace FieldLink.Robot.Protocols.Shared;

/// <summary>클라이언트 열기·닫기·해제 수명 주기를 비동기 잠금과 참조 계수로 안전하게 직렬화합니다.</summary>
internal sealed class ProtocolLifecycleGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private int _referenceCount = 1;
    private int _isDisposed;
    private int _isSemaphoreDisposed;

    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    public async Task<bool> TryEnterAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _referenceCount);
        bool acquired = false;
        try
        {
            if (IsDisposed)
            {
                return false;
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            if (IsDisposed)
            {
                _semaphore.Release();
                acquired = false;
                return false;
            }

            return true;
        }
        finally
        {
            if (!acquired)
            {
                ReleaseReference();
            }
        }
    }

    public void Exit()
    {
        _semaphore.Release();
        ReleaseReference();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        ReleaseReference();
    }

    private void ReleaseReference()
    {
        if (Interlocked.Decrement(ref _referenceCount) == 0
            && Volatile.Read(ref _isDisposed) != 0
            && Interlocked.Exchange(ref _isSemaphoreDisposed, 1) == 0)
        {
            _semaphore.Dispose();
        }
    }
}
