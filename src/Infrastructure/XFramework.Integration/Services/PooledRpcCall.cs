using System.Threading.Tasks.Sources;
using Microsoft.Extensions.ObjectPool;
using StreamFlow.Domain.Shared.Contracts.Requests;

namespace XFramework.Integration.Services;

/// <summary>
/// Zero-allocation RPC completion source backed by an object pool.
/// Implements IValueTaskSource so callers get a ValueTask (no Task allocation).
/// The instance is automatically returned to the pool when GetResult is called.
/// </summary>
public sealed class PooledRpcCall : IValueTaskSource<StreamFlowMessage>
{
    private ManualResetValueTaskSourceCore<StreamFlowMessage> _core;
    private CancellationTokenRegistration _ctr;

    private static readonly ObjectPool<PooledRpcCall> Pool =
        new DefaultObjectPool<PooledRpcCall>(new PooledRpcCallPolicy(), 256);

    public short Version => _core.Version;

    public static PooledRpcCall Rent() => Pool.Get();

    public ValueTask<StreamFlowMessage> GetTask()
        => new(this, _core.Version);

    public void RegisterTimeout(CancellationToken ct)
    {
        if (ct.CanBeCanceled)
        {
            _ctr = ct.Register(static state =>
            {
                var self = (PooledRpcCall)state!;
                self._core.SetException(new TimeoutException("RPC call timed out"));
            }, this);
        }
    }

    public void SetResult(StreamFlowMessage result)
        => _core.SetResult(result);

    public void SetException(Exception ex)
        => _core.SetException(ex);

    StreamFlowMessage IValueTaskSource<StreamFlowMessage>.GetResult(short token)
    {
        try
        {
            return _core.GetResult(token);
        }
        finally
        {
            ReturnToPool();
        }
    }

    ValueTaskSourceStatus IValueTaskSource<StreamFlowMessage>.GetStatus(short token)
        => _core.GetStatus(token);

    void IValueTaskSource<StreamFlowMessage>.OnCompleted(
        Action<object?> continuation, object? state,
        short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);

    private void ReturnToPool()
    {
        _ctr.Dispose();
        _ctr = default;
        _core.Reset();
        Pool.Return(this);
    }
}

file class PooledRpcCallPolicy : IPooledObjectPolicy<PooledRpcCall>
{
    public PooledRpcCall Create() => new();
    public bool Return(PooledRpcCall obj) => true;
}
