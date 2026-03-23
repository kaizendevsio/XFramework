using System.Threading.Tasks.Sources;
using Microsoft.Extensions.ObjectPool;

namespace XFramework.Integration.ThinProtocol;

/// <summary>
/// Zero-allocation RPC completion source for the thin protocol.
/// Same pattern as PooledRpcCall but typed to ThinRpcResponse.
/// </summary>
public sealed class PooledRpcCallThin : IValueTaskSource<ThinRpcResponse>
{
    private ManualResetValueTaskSourceCore<ThinRpcResponse> _core;
    private CancellationTokenRegistration _ctr;

    private static readonly ObjectPool<PooledRpcCallThin> Pool =
        new DefaultObjectPool<PooledRpcCallThin>(new Policy(), 256);

    public short Version => _core.Version;

    public static PooledRpcCallThin Rent() => Pool.Get();

    public ValueTask<ThinRpcResponse> GetTask()
        => new(this, _core.Version);

    public void RegisterTimeout(CancellationToken ct)
    {
        if (ct.CanBeCanceled)
        {
            _ctr = ct.Register(static state =>
            {
                var self = (PooledRpcCallThin)state!;
                self._core.SetException(new TimeoutException("RPC call timed out"));
            }, this);
        }
    }

    public void SetResult(ThinRpcResponse result)
        => _core.SetResult(result);

    public void SetException(Exception ex)
        => _core.SetException(ex);

    ThinRpcResponse IValueTaskSource<ThinRpcResponse>.GetResult(short token)
    {
        try
        {
            return _core.GetResult(token);
        }
        finally
        {
            _ctr.Dispose();
            _ctr = default;
            _core.Reset();
            Pool.Return(this);
        }
    }

    ValueTaskSourceStatus IValueTaskSource<ThinRpcResponse>.GetStatus(short token)
        => _core.GetStatus(token);

    void IValueTaskSource<ThinRpcResponse>.OnCompleted(
        Action<object?> continuation, object? state,
        short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);

    private class Policy : IPooledObjectPolicy<PooledRpcCallThin>
    {
        public PooledRpcCallThin Create() => new();
        public bool Return(PooledRpcCallThin obj) => true;
    }
}
