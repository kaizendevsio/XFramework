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
    private const string AcknowledgeScript = """
        local ackKey = KEYS[1]
        local messageKey = KEYS[2]
        local sequenceKey = KEYS[3]
        local requested = ARGV[1]
        local ttl = tonumber(ARGV[2])
        local current = redis.call('GET', ackKey) or '0'
        local highestIssued = redis.call('GET', sequenceKey) or '0'

        local function compareUnsigned(left, right)
            local leftLength = string.len(left)
            local rightLength = string.len(right)
            if leftLength < rightLength then
                return -1
            end
            if leftLength > rightLength then
                return 1
            end
            if left < right then
                return -1
            end
            if left > right then
                return 1
            end
            return 0
        end

        if string.sub(requested, 1, 1) == '-' or compareUnsigned(requested, current) <= 0 then
            return { 0, current }
        end

        if compareUnsigned(requested, highestIssued) > 0 then
            return { -1, current }
        end

        redis.call('SET', ackKey, requested, 'EX', ttl)
        redis.call('EXPIRE', messageKey, ttl)
        redis.call('EXPIRE', sequenceKey, ttl)
        return { 1, requested }
        """;
    private const string RegisterSubscriberScript = """
        local key = KEYS[1]
        local subscriber = ARGV[1]
        local maximum = tonumber(ARGV[2])
        local ttl = tonumber(ARGV[3])
        if redis.call('SISMEMBER', key, subscriber) == 1 then
            redis.call('EXPIRE', key, ttl)
            return 1
        end
        if redis.call('SCARD', key) >= maximum then
            return 0
        end
        redis.call('SADD', key, subscriber)
        redis.call('EXPIRE', key, ttl)
        return 1
        """;
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
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        var msgKey = MsgKey(topicHash, subscriberId);
        var result = await db.ScriptEvaluateAsync(
            AcknowledgeScript,
            [AckKey(topicHash, subscriberId), msgKey, SeqKey(topicHash, subscriberId)],
            [upToSequence, (long)MessageTtl.TotalSeconds]).WaitAsync(ct);
        var gate = (RedisResult[]?)result
            ?? throw new RedisServerException("Durable ACK validation returned an invalid response.");
        if (gate.Length != 2)
            throw new RedisServerException("Durable ACK validation returned an invalid response.");

        var gateStatus = (long)gate[0];
        if (gateStatus < 0)
            return;
        if (gateStatus > 1)
            throw new RedisServerException("Durable ACK validation returned an unknown status.");

        var effectiveAck = (long)gate[1];
        var entries = await db.StreamRangeAsync(
            msgKey,
            FirstStreamId,
            LastStreamId,
            StreamScanBatchSize,
            Order.Ascending).WaitAsync(ct);
        if (entries.Length == 0)
            return;

        var deleteIds = new List<RedisValue>(entries.Length);
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryReadSequence(entry, out var sequence))
                continue;
            if (sequence > effectiveAck)
                break;

            deleteIds.Add(entry.Id);
        }

        if (deleteIds.Count > 0)
            await db.StreamDeleteAsync(msgKey, deleteIds.ToArray()).WaitAsync(ct);
    }

    public async Task RegisterDurableSubscriberAsync(int topicHash, string subscriberId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var subsKey = SubsKey(topicHash);
        await db.SetAddAsync(subsKey, subscriberId);
        await db.KeyExpireAsync(subsKey, MessageTtl);
    }

    public async Task<bool> TryRegisterDurableSubscriberAsync(
        int topicHash,
        string subscriberId,
        int maxSubscribers,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = await _redis.GetDatabase().ScriptEvaluateAsync(
            RegisterSubscriberScript,
            [SubsKey(topicHash)],
            [subscriberId, Math.Max(1, maxSubscribers), (long)MessageTtl.TotalSeconds]);
        return (long)result == 1;
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

    public async Task<bool> IsDurableSubscriberRegisteredAsync(
        int topicHash,
        string subscriberId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await _redis.GetDatabase().SetContainsAsync(SubsKey(topicHash), subscriberId);
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
