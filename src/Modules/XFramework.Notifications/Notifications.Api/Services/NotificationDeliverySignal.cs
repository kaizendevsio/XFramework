using System.Threading.Channels;

namespace Notifications.Api.Services;

// A wake-up hint only: committed delivery-job rows remain the source of truth. Without it a push
// would wait out the poll interval, which is the difference between a useful notification and one
// that arrives after the person has already opened the app.
public sealed class NotificationDeliverySignal
{
    private readonly Channel<bool> pending = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public void Notify() => pending.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan recoveryInterval, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(recoveryInterval);
        try { await pending.Reader.ReadAsync(timeout.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
    }
}
