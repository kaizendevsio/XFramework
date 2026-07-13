using Bolt.Server.Durable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using StackExchange.Redis;

namespace Bolt.Tests;

[TestFixture]
[Category("RedisIntegration")]
[CancelAfter(30000)]
[NonParallelizable]
public sealed class RedisDurableQueueStoreTests
{
    private const string ConnectionEnvironmentVariable = "BOLT_TEST_REDIS_CONNECTION";
    private const string RequiredEnvironmentVariable = "BOLT_TEST_REDIS_REQUIRED";
    private IConnectionMultiplexer _redis = null!;
    private readonly List<(int TopicHash, string SubscriberId)> _registrations = [];

    [SetUp]
    public async Task SetUp()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var message = $"Real Redis tests require {ConnectionEnvironmentVariable}. " +
                          $"Set {RequiredEnvironmentVariable}=true to make missing configuration fail instead of skip.";
            if (string.Equals(
                    Environment.GetEnvironmentVariable(RequiredEnvironmentVariable),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
                Assert.Fail(message);

            Assert.Ignore(message);
        }

        _redis = await ConnectionMultiplexer.ConnectAsync(connectionString).WaitAsync(TimeSpan.FromSeconds(10));
        await _redis.GetDatabase().PingAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_redis is null)
            return;

        var store = CreateStore(_redis);
        foreach (var registration in _registrations)
        {
            try
            {
                await store.UnregisterDurableSubscriberAsync(
                    registration.TopicHash,
                    registration.SubscriberId).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
            }
        }
        _registrations.Clear();

        await _redis.CloseAsync().WaitAsync(TimeSpan.FromSeconds(5));
        _redis.Dispose();
    }

    [Test]
    public async Task AckAsync_SequencesAboveDoublePrecisionBoundary_PreservesExactInt64Semantics()
    {
        const long doublePrecisionBoundary = 9_007_199_254_740_992L;
        var (store, topicHash, subscriberId) = await CreateRegisteredStoreAsync();
        var db = _redis.GetDatabase();
        await db.StringSetAsync(SequenceKey(topicHash, subscriberId), doublePrecisionBoundary);

        await store.AckAsync(topicHash, subscriberId, doublePrecisionBoundary + 1);

        (await store.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(0);
        ((long)(await db.StringGetAsync(SequenceKey(topicHash, subscriberId)))!).Should().Be(doublePrecisionBoundary);

        var first = await store.AppendAsync(topicHash, subscriberId, new byte[] { 1 });
        var second = await store.AppendAsync(topicHash, subscriberId, new byte[] { 2 });
        first.Should().Be(doublePrecisionBoundary + 1);
        second.Should().Be(doublePrecisionBoundary + 2);

        await store.AckAsync(topicHash, subscriberId, first);

        (await store.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(first);
        var remaining = await ReadAllAsync(store, topicHash, subscriberId);
        remaining.Select(message => message.Sequence).Should().Equal(second);
    }

    [Test]
    public async Task AckAsync_ValidStaleDuplicateAndFutureAcks_AdvanceMonotonicallyAndRejectFutureStateChanges()
    {
        var (store, topicHash, subscriberId) = await CreateRegisteredStoreAsync();
        var first = await store.AppendAsync(topicHash, subscriberId, new byte[] { 1 });
        var second = await store.AppendAsync(topicHash, subscriberId, new byte[] { 2 });
        var third = await store.AppendAsync(topicHash, subscriberId, new byte[] { 3 });

        await store.AckAsync(topicHash, subscriberId, second);
        await store.AckAsync(topicHash, subscriberId, first);
        await store.AckAsync(topicHash, subscriberId, second);

        (await store.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(second);
        var remaining = await ReadAllAsync(store, topicHash, subscriberId);
        remaining.Select(message => message.Sequence).Should().Equal(third);
        var streamLengthBeforeFutureAck = await StreamLengthAsync(topicHash, subscriberId);

        await store.AckAsync(topicHash, subscriberId, long.MaxValue);

        (await store.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(second);
        (await StreamLengthAsync(topicHash, subscriberId)).Should().Be(streamLengthBeforeFutureAck);
        (await ReadAllAsync(store, topicHash, subscriberId)).Select(message => message.Sequence).Should().Equal(third);
    }

    [Test]
    public async Task AckAsync_LargeRetainedQueue_RemovesAtMostOneConfiguredCleanupBatchPerCall()
    {
        const int retainedCount = 257;
        const int cleanupBatchSize = 17;
        var (store, topicHash, subscriberId) = await CreateRegisteredStoreAsync(
            cleanupBatchSize,
            retainedCount + 10);
        long lastSequence = 0;
        for (var index = 0; index < retainedCount; index++)
            lastSequence = await store.AppendAsync(topicHash, subscriberId, new byte[] { (byte)index });

        (await StreamLengthAsync(topicHash, subscriberId)).Should().Be(retainedCount);

        await store.AckAsync(topicHash, subscriberId, lastSequence);

        (await store.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(lastSequence);
        (await StreamLengthAsync(topicHash, subscriberId)).Should().Be(retainedCount - cleanupBatchSize);

        await store.AckAsync(topicHash, subscriberId, lastSequence);

        (await store.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(lastSequence);
        (await StreamLengthAsync(topicHash, subscriberId)).Should().Be(retainedCount - (2 * cleanupBatchSize));
    }

    [Test]
    public async Task AckAsync_PreCanceledToken_DoesNotRunAtomicGate()
    {
        var (store, topicHash, subscriberId) = await CreateRegisteredStoreAsync();
        var sequence = await store.AppendAsync(topicHash, subscriberId, new byte[] { 1 });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = async () => await store.AckAsync(
            topicHash,
            subscriberId,
            sequence,
            cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await store.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(0);
        (await StreamLengthAsync(topicHash, subscriberId)).Should().Be(1);
    }

    [Test]
    public async Task AckAsync_CanceledDuringCleanup_RetryAsDuplicateCompletesOneBatch()
    {
        var (realStore, topicHash, subscriberId) = await CreateRegisteredStoreAsync();
        var sequence = await realStore.AppendAsync(topicHash, subscriberId, new byte[] { 1 });
        var realDatabase = _redis.GetDatabase();
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedCleanup = new TaskCompletionSource<StreamEntry[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellableDatabase = Substitute.For<IDatabase>();
        cancellableDatabase
            .ScriptEvaluateAsync(
                Arg.Any<string>(),
                Arg.Any<RedisKey[]>(),
                Arg.Any<RedisValue[]>(),
                Arg.Any<CommandFlags>())
            .Returns(callInfo => realDatabase.ScriptEvaluateAsync(
                callInfo.ArgAt<string>(0),
                callInfo.ArgAt<RedisKey[]>(1),
                callInfo.ArgAt<RedisValue[]>(2),
                callInfo.ArgAt<CommandFlags>(3)));
        cancellableDatabase
            .StreamRangeAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue?>(),
                Arg.Any<RedisValue?>(),
                Arg.Any<int?>(),
                Arg.Any<Order>(),
                Arg.Any<CommandFlags>())
            .Returns(_ =>
            {
                cleanupStarted.TrySetResult();
                return blockedCleanup.Task;
            });
        var cancellableMultiplexer = Substitute.For<IConnectionMultiplexer>();
        cancellableMultiplexer
            .GetDatabase(Arg.Any<int>(), Arg.Any<object?>())
            .Returns(cancellableDatabase);
        var cancellableStore = CreateStore(cancellableMultiplexer);
        using var cancellation = new CancellationTokenSource();

        var ackTask = cancellableStore.AckAsync(topicHash, subscriberId, sequence, cancellation.Token);
        await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();

        var act = async () => await ackTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
        (await realStore.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(sequence);
        (await StreamLengthAsync(topicHash, subscriberId)).Should().Be(1);

        await realStore.AckAsync(topicHash, subscriberId, sequence);

        (await realStore.GetLastAckedSequenceAsync(topicHash, subscriberId)).Should().Be(sequence);
        (await StreamLengthAsync(topicHash, subscriberId)).Should().Be(0);
    }

    private async Task<(RedisDurableQueueStore Store, int TopicHash, string SubscriberId)> CreateRegisteredStoreAsync(
        int streamScanBatchSize = 2,
        int maxQueueSize = 100)
    {
        var store = CreateStore(_redis, streamScanBatchSize, maxQueueSize);
        var topicHash = Random.Shared.Next(1, int.MaxValue);
        var subscriberId = $"phase0-test-{Guid.NewGuid():N}";
        await store.RegisterDurableSubscriberAsync(topicHash, subscriberId);
        _registrations.Add((topicHash, subscriberId));
        return (store, topicHash, subscriberId);
    }

    private static RedisDurableQueueStore CreateStore(
        IConnectionMultiplexer redis,
        int streamScanBatchSize = 2,
        int maxQueueSize = 100) =>
        new(
            redis,
            Options.Create(new DurableQueueOptions
            {
                MaxQueueSize = maxQueueSize,
                MessageTtlSeconds = 300,
                RedisStreamScanBatchSize = streamScanBatchSize
            }),
            NullLogger<RedisDurableQueueStore>.Instance);

    private async Task<long> StreamLengthAsync(int topicHash, string subscriberId) =>
        await _redis.GetDatabase().StreamLengthAsync(MessageKey(topicHash, subscriberId));

    private static string MessageKey(int topicHash, string subscriberId) =>
        $"bolt:durable:msg:{topicHash}:{subscriberId}";

    private static string SequenceKey(int topicHash, string subscriberId) =>
        $"bolt:durable:seq:{topicHash}:{subscriberId}";

    private static async Task<List<(long Sequence, byte[] Payload)>> ReadAllAsync(
        IDurableQueueStore store,
        int topicHash,
        string subscriberId,
        long fromSequence = 0)
    {
        var messages = new List<(long Sequence, byte[] Payload)>();
        await foreach (var message in store.ReadFromAsync(topicHash, subscriberId, fromSequence, 1_000))
            messages.Add(message);
        return messages;
    }
}
