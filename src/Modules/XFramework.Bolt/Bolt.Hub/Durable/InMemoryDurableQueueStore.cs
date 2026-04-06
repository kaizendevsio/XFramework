using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bolt.Hub.Durable;

/// <summary>
/// In-process durable queue store. Messages do not survive Hub restarts.
/// Used as a fallback when Redis is not configured.
/// </summary>
public sealed class InMemoryDurableQueueStore : IDurableQueueStore
{
    private readonly DurableQueueOptions _options;
    private readonly ILogger<InMemoryDurableQueueStore> _logger;

    // Per-(topicHash, subscriberId) queue with its own lock and sequence counter
    private readonly ConcurrentDictionary<(int TopicHash, string SubscriberId), QueueState> _queues = new();

    // Per-topic set of registered durable subscriberIds
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> _subscribers = new();

    public InMemoryDurableQueueStore(IOptions<DurableQueueOptions> options, ILogger<InMemoryDurableQueueStore> logger)
    {
        _options = options.Value;
        _logger = logger;
        _logger.LogWarning("Using in-memory durable queue store. Messages will be lost on Hub restart. Configure Redis for production.");
    }

    public Task<long> AppendAsync(int topicHash, string subscriberId, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var state = _queues.GetOrAdd((topicHash, subscriberId), _ => new QueueState());
        long seq;
        lock (state.Lock)
        {
            seq = ++state.NextSequence;
            state.Messages.Add((seq, payload.ToArray()));
            // Trim to MaxQueueSize
            while (state.Messages.Count > _options.MaxQueueSize)
                state.Messages.RemoveAt(0);
        }
        return Task.FromResult(seq);
    }

    public async IAsyncEnumerable<(long Sequence, byte[] Payload)> ReadFromAsync(int topicHash, string subscriberId, long fromSequence, int maxCount, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_queues.TryGetValue((topicHash, subscriberId), out var state))
            yield break;

        List<(long, byte[])> snapshot;
        lock (state.Lock)
        {
            snapshot = state.Messages
                .Where(m => m.Sequence > fromSequence)
                .Take(maxCount)
                .ToList();
        }

        foreach (var msg in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return msg;
            await Task.Yield();
        }
    }

    public Task AckAsync(int topicHash, string subscriberId, long upToSequence, CancellationToken ct = default)
    {
        if (_queues.TryGetValue((topicHash, subscriberId), out var state))
        {
            lock (state.Lock)
            {
                state.Messages.RemoveAll(m => m.Sequence <= upToSequence);
                if (upToSequence > state.LastAckedSequence)
                    state.LastAckedSequence = upToSequence;
            }
        }
        return Task.CompletedTask;
    }

    public Task RegisterDurableSubscriberAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        var set = _subscribers.GetOrAdd(topicHash, _ => new ConcurrentDictionary<string, byte>());
        set.TryAdd(subscriberId, 0);
        // Ensure queue state exists
        _queues.GetOrAdd((topicHash, subscriberId), _ => new QueueState());
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetDurableSubscribersAsync(int topicHash, CancellationToken ct = default)
    {
        if (_subscribers.TryGetValue(topicHash, out var set))
            return Task.FromResult<IReadOnlyList<string>>(set.Keys.ToList());
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task<long> GetLastAckedSequenceAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        if (_queues.TryGetValue((topicHash, subscriberId), out var state))
        {
            lock (state.Lock)
                return Task.FromResult(state.LastAckedSequence);
        }
        return Task.FromResult(0L);
    }

    private sealed class QueueState
    {
        public readonly object Lock = new();
        public long NextSequence;
        public long LastAckedSequence;
        public readonly List<(long Sequence, byte[] Payload)> Messages = new();
    }
}
