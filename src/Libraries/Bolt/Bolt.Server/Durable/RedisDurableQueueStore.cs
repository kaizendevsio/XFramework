using System.Buffers.Binary;
using System.Globalization;
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
    private const string AppendScript = """
        local messageKey = KEYS[1]
        local sequenceKey = KEYS[2]
        local bytesKey = KEYS[3]
        local subscribersKey = KEYS[4]
        local payload = ARGV[1]
        local maximumCount = tonumber(ARGV[2])
        local maximumBytes = tonumber(ARGV[3])
        local ttl = tonumber(ARGV[4])
        local payloadSize = string.len(payload)

        local function entrySize(fields)
            local size = 0
            for index = 1, #fields, 2 do
                if fields[index] == 'size' then
                    size = tonumber(fields[index + 1]) or 0
                elseif fields[index] == 'payload' and size == 0 then
                    size = string.len(fields[index + 1])
                end
            end
            return size
        end

        if payloadSize > maximumBytes then
            return { 0, tostring(payloadSize) }
        end

        local retainedBytesValue = redis.call('GET', bytesKey)
        local retainedBytes = tonumber(retainedBytesValue or '0')
        local retainedCount = redis.call('XLEN', messageKey)
        if retainedCount == 0 then
            retainedBytes = 0
        elseif not retainedBytesValue then
            local existing = redis.call('XRANGE', messageKey, '-', '+')
            for index = 1, #existing do
                retainedBytes = retainedBytes + entrySize(existing[index][2])
            end
        end

        while retainedCount > 0 and
              (retainedCount >= maximumCount or retainedBytes + payloadSize > maximumBytes) do
            local oldest = redis.call('XRANGE', messageKey, '-', '+', 'COUNT', 1)
            if #oldest == 0 then
                retainedBytes = 0
                retainedCount = 0
                break
            end

            local oldestSize = entrySize(oldest[1][2])

            redis.call('XDEL', messageKey, oldest[1][1])
            retainedBytes = math.max(0, retainedBytes - oldestSize)
            retainedCount = retainedCount - 1
        end

        redis.call('INCR', sequenceKey)
        local sequence = redis.call('GET', sequenceKey)
        redis.call(
            'XADD', messageKey, '*',
            'seq', sequence,
            'payload', payload,
            'size', tostring(payloadSize))
        redis.call('SET', bytesKey, tostring(retainedBytes + payloadSize), 'EX', ttl)
        redis.call('EXPIRE', messageKey, ttl)
        redis.call('EXPIRE', sequenceKey, ttl)
        redis.call('EXPIRE', subscribersKey, ttl)
        return { 1, sequence }
        """;
    private const string DeleteAcknowledgedEntriesScript = """
        local bytesKey = KEYS[1]
        local messageKey = KEYS[2]
        local ttl = tonumber(ARGV[1])
        local retainedBytesValue = redis.call('GET', bytesKey)
        local retainedBytes = tonumber(retainedBytesValue or '0')
        if not retainedBytesValue then
            local existing = redis.call('XRANGE', messageKey, '-', '+')
            for entryIndex = 1, #existing do
                local fields = existing[entryIndex][2]
                for fieldIndex = 1, #fields, 2 do
                    if fields[fieldIndex] == 'payload' then
                        retainedBytes = retainedBytes + string.len(fields[fieldIndex + 1])
                        break
                    end
                end
            end
        end

        for index = 2, #ARGV, 2 do
            local deleted = redis.call('XDEL', messageKey, ARGV[index])
            if deleted == 1 then
                retainedBytes = math.max(0, retainedBytes - tonumber(ARGV[index + 1]))
            end
        end

        redis.call('SET', bytesKey, tostring(retainedBytes), 'EX', ttl)
        return retainedBytes
        """;
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
    private static string BytesKey(int topicHash, string subscriberId) => $"bolt:durable:bytes:{topicHash}:{subscriberId}";
    private TimeSpan MessageTtl => TimeSpan.FromSeconds(Math.Max(60, _options.MessageTtlSeconds));
    private int StreamScanBatchSize => Math.Max(1, _options.RedisStreamScanBatchSize);

    public async Task<long> AppendAsync(int topicHash, string subscriberId, ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        var msgKey = MsgKey(topicHash, subscriberId);
        var seqKey = SeqKey(topicHash, subscriberId);
        var maxQueueBytes = Math.Max(1, _options.MaxQueueBytesPerSubscriber);
        if (payload.Length > maxQueueBytes)
        {
            throw new BoltDurableQueueByteCapacityExceededException(
                $"Durable payload size {payload.Length} exceeds the per-subscriber byte capacity {maxQueueBytes}.");
        }

        var result = await db.ScriptEvaluateAsync(
            AppendScript,
            [msgKey, seqKey, BytesKey(topicHash, subscriberId), SubsKey(topicHash)],
            [
                payload.ToArray(),
                Math.Max(1, _options.MaxQueueSize),
                maxQueueBytes,
                (long)MessageTtl.TotalSeconds
            ]).WaitAsync(ct);
        var append = (RedisResult[]?)result
            ?? throw new RedisServerException("Durable append returned an invalid response.");
        if (append.Length != 2)
            throw new RedisServerException("Durable append returned an invalid response.");
        if ((long)append[0] == 0)
        {
            throw new BoltDurableQueueByteCapacityExceededException(
                $"Durable payload size {payload.Length} exceeds the per-subscriber byte capacity {maxQueueBytes}.");
        }

        if (!long.TryParse(append[1].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
            throw new RedisServerException("Durable append returned an invalid sequence.");
        return sequence;
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

        var deleteEntries = new List<(RedisValue Id, int PayloadBytes)>(entries.Length);
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryReadSequence(entry, out var sequence))
                continue;
            if (sequence > effectiveAck)
                break;

            var payloadValue = entry.Values.FirstOrDefault(v => v.Name == "payload").Value;
            deleteEntries.Add((entry.Id, payloadValue.IsNull ? 0 : ((byte[])payloadValue!).Length));
        }

        if (deleteEntries.Count > 0)
        {
            var arguments = new RedisValue[1 + (deleteEntries.Count * 2)];
            arguments[0] = (long)MessageTtl.TotalSeconds;
            for (var index = 0; index < deleteEntries.Count; index++)
            {
                arguments[1 + (index * 2)] = deleteEntries[index].Id;
                arguments[2 + (index * 2)] = deleteEntries[index].PayloadBytes;
            }

            await db.ScriptEvaluateAsync(
                DeleteAcknowledgedEntriesScript,
                [BytesKey(topicHash, subscriberId), msgKey],
                arguments).WaitAsync(ct);
        }
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
        await db.KeyDeleteAsync(BytesKey(topicHash, subscriberId));
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

        if (long.TryParse(seqValue.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out sequence))
            return true;

        var seqBytes = (byte[])seqValue!;
        if (seqBytes.Length != sizeof(long))
            return false;

        sequence = BinaryPrimitives.ReadInt64LittleEndian(seqBytes);
        return sequence >= 0;
    }
}
