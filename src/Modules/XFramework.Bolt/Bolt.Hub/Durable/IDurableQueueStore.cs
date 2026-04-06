namespace Bolt.Hub.Durable;

/// <summary>
/// Backend for durable subscription queues. Each (topicHash, subscriberId) has its own
/// monotonically-increasing sequence-numbered queue.
/// </summary>
public interface IDurableQueueStore
{
    /// <summary>
    /// Append a message to the queue for (topicHash, subscriberId). Returns the assigned sequence number.
    /// Trims oldest messages when queue exceeds MaxQueueSize.
    /// </summary>
    Task<long> AppendAsync(int topicHash, string subscriberId, ReadOnlyMemory<byte> payload, CancellationToken ct = default);

    /// <summary>
    /// Read up to maxCount unacked messages starting from (fromSequence + 1).
    /// Returns (sequence, payload) pairs in sequence order.
    /// </summary>
    IAsyncEnumerable<(long Sequence, byte[] Payload)> ReadFromAsync(int topicHash, string subscriberId, long fromSequence, int maxCount, CancellationToken ct = default);

    /// <summary>
    /// Mark all messages up to and including upToSequence as acked. They are removed from the queue.
    /// </summary>
    Task AckAsync(int topicHash, string subscriberId, long upToSequence, CancellationToken ct = default);

    /// <summary>
    /// Idempotently register that (topicHash, subscriberId) is a durable subscriber for this topic.
    /// Future publishes to this topic will enqueue for this subscriber.
    /// </summary>
    Task RegisterDurableSubscriberAsync(int topicHash, string subscriberId, CancellationToken ct = default);

    /// <summary>
    /// Get all subscriberIds currently registered as durable for the given topic.
    /// Used by publish to know which queues to enqueue into.
    /// </summary>
    Task<IReadOnlyList<string>> GetDurableSubscribersAsync(int topicHash, CancellationToken ct = default);

    /// <summary>
    /// Get the last sequence number this subscriber acked. Returns 0 if no ack yet.
    /// Used on reconnect to find the starting point for replay.
    /// </summary>
    Task<long> GetLastAckedSequenceAsync(int topicHash, string subscriberId, CancellationToken ct = default);
}
