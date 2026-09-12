using System.Threading.Channels;

namespace Storage.Api.Services;

// The database remains the durable queue. This coalesced wake-up only avoids
// waiting for the recovery poll when an upload has just become verifiable.
public sealed class StorageMaintenanceSignal
{
    private readonly Channel<bool> pending = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropWrite
    });

    public void Notify() => pending.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan interval, TimeProvider timeProvider, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(interval, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try { await pending.Reader.ReadAsync(linked.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
    }
}
