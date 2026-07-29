using System.Threading.Tasks.Sources;
using Microsoft.Extensions.ObjectPool;

namespace Bolt.Client;

/// <summary>
/// Zero-allocation RPC completion source for the thin protocol.
/// Same pattern as PooledRpcCall but typed to BoltRpcResponse.
/// </summary>
public sealed class PooledRpcCall : IValueTaskSource<BoltRpcResponse>
{
    private ManualResetValueTaskSourceCore<BoltRpcResponse> _core;
    private CancellationTokenRegistration _ctr;
    private BoltClient? _cancellationOwner;
    private Guid _requestId;
    private CancellationToken _cancellationToken;
    private int _completed;

    private static readonly ObjectPool<PooledRpcCall> Pool =
        new DefaultObjectPool<PooledRpcCall>(new Policy(), 256);

    public short Version => _core.Version;

    public static PooledRpcCall Rent(bool runContinuationsAsynchronously = false)
    {
        var call = Pool.Get();
        call.ResetForRent(runContinuationsAsynchronously);
        return call;
    }

    public ValueTask<BoltRpcResponse> GetTask()
        => new(this, _core.Version);

    public void RegisterTimeout(CancellationToken ct)
    {
        if (ct.CanBeCanceled)
        {
            _ctr = ct.Register(static state =>
            {
                var self = (PooledRpcCall)state!;
                self.SetException(new TimeoutException("RPC call timed out"));
            }, this);
        }
    }

    internal void RegisterCancellation(
        CancellationToken ct,
        BoltClient owner,
        Guid requestId)
    {
        if (!ct.CanBeCanceled)
            return;

        _cancellationOwner = owner;
        _requestId = requestId;
        _cancellationToken = ct;
        _ctr = ct.UnsafeRegister(static state =>
        {
            var self = (PooledRpcCall)state!;
            self._cancellationOwner?.CancelPendingCall(
                self._requestId,
                self,
                self._cancellationToken);
        }, this);
    }

    public void SetResult(BoltRpcResponse result)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;

        _core.SetResult(result);
    }

    public void SetException(Exception ex)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;

        _core.SetException(ex);
    }

    BoltRpcResponse IValueTaskSource<BoltRpcResponse>.GetResult(short token)
    {
        try
        {
            return _core.GetResult(token);
        }
        finally
        {
            _ctr.Dispose();
            _ctr = default;
            _cancellationOwner = null;
            _requestId = default;
            _cancellationToken = default;
            Pool.Return(this);
        }
    }

    ValueTaskSourceStatus IValueTaskSource<BoltRpcResponse>.GetStatus(short token)
        => _core.GetStatus(token);

    void IValueTaskSource<BoltRpcResponse>.OnCompleted(
        Action<object?> continuation, object? state,
        short token, ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);

    private class Policy : IPooledObjectPolicy<PooledRpcCall>
    {
        public PooledRpcCall Create() => new();
        public bool Return(PooledRpcCall obj) => true;
    }

    private void ResetForRent(bool runContinuationsAsynchronously)
    {
        _core.RunContinuationsAsynchronously = runContinuationsAsynchronously;
        _core.Reset();
        _ctr = default;
        _cancellationOwner = null;
        _requestId = default;
        _cancellationToken = default;
        _completed = 0;
    }
}
