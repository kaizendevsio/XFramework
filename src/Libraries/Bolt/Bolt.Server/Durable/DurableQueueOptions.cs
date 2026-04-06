namespace Bolt.Server.Durable;

/// <summary>
/// Configuration for durable subscription queues.
/// </summary>
public sealed class DurableQueueOptions
{
    /// <summary>Optional Redis connection string. If null, in-memory store is used.</summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>Time-to-live for queued messages in seconds. Default 7 days.</summary>
    public int MessageTtlSeconds { get; set; } = 604_800;

    /// <summary>Maximum messages per (topic, subscriber) queue. Oldest are dropped when exceeded.</summary>
    public int MaxQueueSize { get; set; } = 10_000;

    /// <summary>Maximum messages replayed in a single batch on reconnect.</summary>
    public int MaxReplayBatchSize { get; set; } = 1_000;
}
