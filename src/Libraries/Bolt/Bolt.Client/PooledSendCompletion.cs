using System.Threading.Tasks.Sources;
using Microsoft.Extensions.ObjectPool;

namespace Bolt.Client;

internal sealed class PooledSendCompletion : IValueTaskSource
{
    private static readonly ObjectPool<PooledSendCompletion> Pool =
        new DefaultObjectPool<PooledSendCompletion>(new Policy(), 256);

    private ManualResetValueTaskSourceCore<bool> _core;
    private CancellationTokenRegistration _cancellationRegistration;
    private CancellationToken _callerToken;
    private int _completionSignaled;
    private int _transportCompleted;
    private int _waiterConsumed;
    private int _returned;

    private PooledSendCompletion()
    {
        _core.RunContinuationsAsynchronously = true;
    }

    public static PooledSendCompletion Rent()
    {
        var completion = Pool.Get();
        completion._core.Reset();
        completion._cancellationRegistration = default;
        completion._callerToken = default;
        completion._completionSignaled = 0;
        completion._transportCompleted = 0;
        completion._waiterConsumed = 0;
        completion._returned = 0;
        return completion;
    }

    public ValueTask WaitAsync(CancellationToken cancellationToken)
    {
        _callerToken = cancellationToken;
        if (cancellationToken.CanBeCanceled)
        {
            _cancellationRegistration = cancellationToken.UnsafeRegister(
                static state => ((PooledSendCompletion)state!).CancelWaiter(),
                this);
        }

        return new ValueTask(this, _core.Version);
    }

    public void SetResult()
    {
        if (Interlocked.CompareExchange(ref _completionSignaled, 1, 0) == 0)
            _core.SetResult(true);

        Volatile.Write(ref _transportCompleted, 1);
        TryReturn();
    }

    public void SetException(Exception exception)
    {
        if (Interlocked.CompareExchange(ref _completionSignaled, 1, 0) == 0)
            _core.SetException(exception);

        Volatile.Write(ref _transportCompleted, 1);
        TryReturn();
    }

    public void SetCanceled(CancellationToken cancellationToken) =>
        SetException(new OperationCanceledException(cancellationToken));

    public void ReturnUnused()
    {
        Volatile.Write(ref _transportCompleted, 1);
        Volatile.Write(ref _waiterConsumed, 1);
        TryReturn();
    }

    void IValueTaskSource.GetResult(short token)
    {
        try
        {
            _core.GetResult(token);
        }
        finally
        {
            _cancellationRegistration.Dispose();
            _cancellationRegistration = default;
            Volatile.Write(ref _waiterConsumed, 1);
            TryReturn();
        }
    }

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) => _core.GetStatus(token);

    void IValueTaskSource.OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags) =>
        _core.OnCompleted(continuation, state, token, flags);

    private void CancelWaiter()
    {
        if (Interlocked.CompareExchange(ref _completionSignaled, 1, 0) == 0)
            _core.SetException(new OperationCanceledException(_callerToken));
    }

    private void TryReturn()
    {
        if (Volatile.Read(ref _transportCompleted) == 0 ||
            Volatile.Read(ref _waiterConsumed) == 0 ||
            Interlocked.Exchange(ref _returned, 1) != 0)
        {
            return;
        }

        _callerToken = default;
        Pool.Return(this);
    }

    private sealed class Policy : IPooledObjectPolicy<PooledSendCompletion>
    {
        public PooledSendCompletion Create() => new();
        public bool Return(PooledSendCompletion obj) => true;
    }
}
