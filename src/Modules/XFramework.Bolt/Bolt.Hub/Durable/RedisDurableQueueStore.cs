using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Bolt.Hub.Durable;

/// <summary>
/// Redis-backed durable queue store using Redis Streams.
///
/// Key conventions:
/// - bolt:durable:msg:{topicHash}:{subscriberId}  (stream)  — message queue
/// - bolt:durable:subs:{topicHash}                (set)     — registered subscriberIds
/// - bolt:durable:seq:{topicHash}:{subscriberId}  (string)  — monotonic counter
/// - bolt:durable:ack:{topicHash}:{subscriberId}  (string)  — last acked sequence
/// </summary>
public sealed class RedisDurableQueueStore : IDurableQueueStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly DurableQueueOptions _options;
    private readonly ILogger<RedisDurableQueueStore> _logger;

    public RedisDurableQueueStore(IConnectionMultiplexer redis, IOptions<DurableQueueOptions> options, ILogger<RedisDurableQueueStore> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    private static string MsgKey(int topicHash, string subscriberId) => $"bolt:durable:msg:{topicHash}:{subscriberId}";
    private static string SubsKey(int topicHash) => $"bolt:durable:subs:{topicHash}";
    private static string SeqKey(int topicHash, string subscriberId) => $"bolt:durable:seq:{topicHash}:{subscriberId}";
    private static string AckKey(int topicHash, string subscriberId) => $"bolt:durable:ack:{topicHash}:{subscriberId}";

    public async Task<long> AppendAsync(int topicHash, string subscriberId, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var seq = await db.StringIncrementAsync(SeqKey(topicHash, subscriberId));

        var seqBytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(seqBytes, seq);

        await db.StreamAddAsync(
            MsgKey(topicHash, subscriberId),
            new NameValueEntry[]
            {
                new("seq", seqBytes),
                new("payload", payload.ToArray())
            },
            maxLength: _options.MaxQueueSize,
            useApproximateMaxLength: true);

        return seq;
    }

    public async IAsyncEnumerable<(long Sequence, byte[] Payload)> ReadFromAsync(int topicHash, string subscriberId, long fromSequence, int maxCount, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var entries = await db.StreamRangeAsync(MsgKey(topicHash, subscriberId), count: maxCount);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            var seqValue = entry.Values.FirstOrDefault(v => v.Name == "seq").Value;
            var payloadValue = entry.Values.FirstOrDefault(v => v.Name == "payload").Value;
            if (seqValue.IsNullOrEmpty || payloadValue.IsNullOrEmpty) continue;

            var seqBytes = (byte[])seqValue!;
            var seq = BinaryPrimitives.ReadInt64LittleEndian(seqBytes);
            if (seq <= fromSequence) continue;

            yield return (seq, (byte[])payloadValue!);
        }
    }

    public async Task AckAsync(int topicHash, string subscriberId, long upToSequence, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        // Update last acked
        await db.StringSetAsync(AckKey(topicHash, subscriberId), upToSequence);

        // Delete entries with seq <= upToSequence
        var entries = await db.StreamRangeAsync(MsgKey(topicHash, subscriberId));
        var toDelete = new List<RedisValue>();
        foreach (var entry in entries)
        {
            var seqValue = entry.Values.FirstOrDefault(v => v.Name == "seq").Value;
            if (seqValue.IsNullOrEmpty) continue;
            var seq = BinaryPrimitives.ReadInt64LittleEndian((byte[])seqValue!);
            if (seq <= upToSequence)
                toDelete.Add(entry.Id);
        }
        if (toDelete.Count > 0)
            await db.StreamDeleteAsync(MsgKey(topicHash, subscriberId), toDelete.ToArray());
    }

    public async Task RegisterDurableSubscriberAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.SetAddAsync(SubsKey(topicHash), subscriberId);
    }

    public async Task<IReadOnlyList<string>> GetDurableSubscribersAsync(int topicHash, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var members = await db.SetMembersAsync(SubsKey(topicHash));
        return members.Select(m => m.ToString()).ToList();
    }

    public async Task<long> GetLastAckedSequenceAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(AckKey(topicHash, subscriberId));
        return value.IsNullOrEmpty ? 0L : (long)value;
    }
}
