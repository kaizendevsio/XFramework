using System.Threading.Channels;

namespace Communications.Api.Services;

// A wake-up hint only: committed database rows remain the source of truth.
public sealed class CommunicationsOutboxSignal
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
