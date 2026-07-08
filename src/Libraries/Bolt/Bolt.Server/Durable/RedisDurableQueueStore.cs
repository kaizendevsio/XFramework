using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Bolt.Server.Durable;

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
    private const string FirstStreamId = "-";
    private const string LastStreamId = "+";

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
    private TimeSpan MessageTtl => TimeSpan.FromSeconds(Math.Max(60, _options.MessageTtlSeconds));
    private int StreamScanBatchSize => Math.Max(1, _options.RedisStreamScanBatchSize);

    public async Task<long> AppendAsync(int topicHash, string subscriberId, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var msgKey = MsgKey(topicHash, subscriberId);
        var seqKey = SeqKey(topicHash, subscriberId);
        var seq = await db.StringIncrementAsync(SeqKey(topicHash, subscriberId));

        var seqBytes = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(seqBytes, seq);

        await db.StreamAddAsync(
            msgKey,
            new NameValueEntry[]
            {
                new("seq", seqBytes),
                new("payload", payload.ToArray())
            },
            maxLength: _options.MaxQueueSize,
            useApproximateMaxLength: true);

        await db.KeyExpireAsync(msgKey, MessageTtl);
        await db.KeyExpireAsync(seqKey, MessageTtl);
        await db.KeyExpireAsync(SubsKey(topicHash), MessageTtl);

        return seq;
    }

    public async IAsyncEnumerable<(long Sequence, byte[] Payload)> ReadFromAsync(int topicHash, string subscriberId, long fromSequence, int maxCount, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var remaining = Math.Max(1, maxCount);
        var msgKey = MsgKey(topicHash, subscriberId);
        RedisValue minId = FirstStreamId;

        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();

            var entries = await db.StreamRangeAsync(
                msgKey,
                minId,
                LastStreamId,
                StreamScanBatchSize,
                Order.Ascending);

            if (entries.Length == 0)
                yield break;

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                minId = ExcludeStreamId(entry.Id);

                if (!TryReadSequence(entry, out var seq) || seq <= fromSequence)
                    continue;

                var payloadValue = entry.Values.FirstOrDefault(v => v.Name == "payload").Value;
                if (payloadValue.IsNullOrEmpty)
                    continue;

                yield return (seq, (byte[])payloadValue!);
                remaining--;
                if (remaining == 0)
                    yield break;
            }

            if (entries.Length < StreamScanBatchSize)
                yield break;
        }
    }

    public async Task AckAsync(int topicHash, string subscriberId, long upToSequence, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        // Update last acked
        var ackKey = AckKey(topicHash, subscriberId);
        var msgKey = MsgKey(topicHash, subscriberId);
        await db.StringSetAsync(ackKey, upToSequence);
        await db.KeyExpireAsync(ackKey, MessageTtl);

        var toDelete = new List<RedisValue>();
        RedisValue minId = FirstStreamId;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var entries = await db.StreamRangeAsync(
                msgKey,
                minId,
                LastStreamId,
                StreamScanBatchSize,
                Order.Ascending);

            if (entries.Length == 0)
                break;

            foreach (var entry in entries)
            {
                minId = ExcludeStreamId(entry.Id);

                if (!TryReadSequence(entry, out var seq))
                    continue;

                if (seq > upToSequence)
                {
                    if (toDelete.Count > 0)
                        await db.StreamDeleteAsync(msgKey, toDelete.ToArray());

                    await db.KeyExpireAsync(msgKey, MessageTtl);
                    return;
                }

                toDelete.Add(entry.Id);
            }

            if (toDelete.Count > 0)
            {
                await db.StreamDeleteAsync(msgKey, toDelete.ToArray());
                toDelete.Clear();
            }

            if (entries.Length < StreamScanBatchSize)
                break;
        }

        await db.KeyExpireAsync(msgKey, MessageTtl);
    }

    public async Task RegisterDurableSubscriberAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var subsKey = SubsKey(topicHash);
        await db.SetAddAsync(subsKey, subscriberId);
        await db.KeyExpireAsync(subsKey, MessageTtl);
    }

    public async Task UnregisterDurableSubscriberAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.SetRemoveAsync(SubsKey(topicHash), subscriberId);
        await db.KeyDeleteAsync(MsgKey(topicHash, subscriberId));
        await db.KeyDeleteAsync(SeqKey(topicHash, subscriberId));
        await db.KeyDeleteAsync(AckKey(topicHash, subscriberId));
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

    private static RedisValue ExcludeStreamId(RedisValue streamId) => $"({streamId}";

    private static bool TryReadSequence(StreamEntry entry, out long sequence)
    {
        sequence = 0;
        var seqValue = entry.Values.FirstOrDefault(v => v.Name == "seq").Value;
        if (seqValue.IsNullOrEmpty)
            return false;

        var seqBytes = (byte[])seqValue!;
        if (seqBytes.Length < sizeof(long))
            return false;

        sequence = BinaryPrimitives.ReadInt64LittleEndian(seqBytes);
        return true;
    }
}
