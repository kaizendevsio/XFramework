namespace Bolt.Client;

/// <summary>
/// Wraps a durable message payload with its sequence number and replay flag.
/// Carries an Ack helper that calls back into the originating BoltClient.
/// </summary>
public sealed class DurableMessage<T>
{
    private readonly Func<long, CancellationToken, ValueTask> _ackCallback;

    public T Payload { get; }
    public long Sequence { get; }
    public bool IsReplay { get; }

    internal DurableMessage(T payload, long sequence, bool isReplay, Func<long, CancellationToken, ValueTask> ackCallback)
    {
        Payload = payload;
        Sequence = sequence;
        IsReplay = isReplay;
        _ackCallback = ackCallback;
    }

    /// <summary>Acknowledge this message (and all earlier ones from the same subscriber).</summary>
    public ValueTask AckAsync(CancellationToken ct = default) => _ackCallback(Sequence, ct);
}
