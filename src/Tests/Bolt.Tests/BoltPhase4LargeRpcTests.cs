using System.Net;
using System.Reflection;
using Bolt.Client;
using Bolt.Server;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Bolt.Tests;

[TestFixture]
[NonParallelizable]
[CancelAfter(180_000)]
public sealed class BoltPhase4LargeRpcTests
{
    private const int MiB = 1024 * 1024;
    private const int MaxLargeRpcPayloadBytes = 32 * MiB;
    private static int _portCounter = 24_000;

    private WebApplication _serverApp = null!;
    private ILoggerFactory _loggerFactory = null!;
    private int _clientSequence;
    private int _port;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _port = Interlocked.Increment(ref _portCounter);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Services.AddBoltServer(options =>
        {
            options.InvocationTimeoutMs = 120_000;
            options.MaxLargeRpcPayloadBytes = MaxLargeRpcPayloadBytes;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _serverApp = builder.Build();
        _serverApp.UseWebSockets();
        _serverApp.MapBolt("/bolt");
        _serverApp.MapGet("/health", () => "ok");

        _ = Task.Run(() => _serverApp.RunAsync());
        await WaitForHealthAsync($"http://localhost:{_port}/health");
        _loggerFactory = _serverApp.Services.GetRequiredService<ILoggerFactory>();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        try { await _serverApp.StopAsync(); } catch { }
        try { await _serverApp.DisposeAsync(); } catch { }
    }

    [Test]
    public void StreamChunkSize_DefaultsTo128KiBFrameAligned()
    {
        new BoltClientOptions().StreamChunkSize.Should().Be(
            (128 * 1024) - Bolt.Protocol.BoltCodec.StreamDataHeaderSize);
    }

    [Test]
    public void LargePayloadThreshold_DefaultsTo2MiB()
    {
        new BoltClientOptions().LargePayloadThreshold.Should().Be(2 * MiB);
    }

    [Test]
    public void LargePayloadSettings_AreClampedToCompleteFrameLimits()
    {
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        var client = new BoltClient(
            new Uri("ws://localhost/bolt"),
            "frame_limit_test",
            "frame_limit_test",
            new BoltClientOptions
            {
                MaxFrameBytes = 64 * 1024,
                LargePayloadThreshold = 5 * MiB,
                StreamChunkSize = 1024 * 1024
            },
            loggerFactory.CreateLogger<BoltClient>());

        GetPrivateInt(client, "_largePayloadThreshold")
            .Should().Be(64 * 1024 - Bolt.Protocol.BoltCodec.RequestHeaderSize - 18);
        GetPrivateInt(client, "_streamChunkSize")
            .Should().Be(64 * 1024 - Bolt.Protocol.BoltCodec.StreamDataHeaderSize);
    }

    [TestCase(256 * 1024, 2 * MiB, 8)]
    [TestCase(512 * 1024, 2 * MiB, 4)]
    [TestCase(1024 * 1024, 2 * MiB, 2)]
    [TestCase(1024 * 1024, 4 * MiB, 4)]
    public void LargeRpcPipeline_IsBoundedByBytes(int chunkSize, int pipelineBytes, int expectedChunks)
    {
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        var client = new BoltClient(
            new Uri("ws://localhost/bolt"),
            "pipeline_limit_test",
            "pipeline_limit_test",
            new BoltClientOptions
            {
                StreamChunkSize = chunkSize,
                MaxLargeRpcPipelineBytes = pipelineBytes
            },
            loggerFactory.CreateLogger<BoltClient>());

        GetPrivateInt(client, "_maxLargeRpcChunksInFlight").Should().Be(expectedChunks);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    [TestCase(10)]
    [TestCase(20)]
    [TestCase(32)]
    public async Task LargeRpc_RoundTripsConfiguredPayloadSizes(int payloadSizeMiB)
    {
        var callerId = NextClientId("phase4_caller");
        var recipientId = NextClientId("phase4_recipient");
        await using var caller = CreateClient(callerId, streamChunkSize: 256 * 1024);
        await using var recipient = CreateClient(recipientId, streamChunkSize: 256 * 1024);

        recipient.RegisterHandler("echo-large", static (payload, _) =>
            Task.FromResult((HttpStatusCode.OK, payload)));

        await caller.ConnectAsync();
        await recipient.ConnectAsync();

        var payload = GC.AllocateUninitializedArray<byte>(payloadSizeMiB * MiB);
        payload.AsSpan().Fill((byte)payloadSizeMiB);

        var (statusCode, response) = await caller
            .InvokeAsync(recipientId, "echo-large", payload)
            .WaitAsync(TimeSpan.FromSeconds(120));

        statusCode.Should().Be(HttpStatusCode.OK);
        response.Span.SequenceEqual(payload).Should().BeTrue();
        await WaitForLargeRpcBuffersToDrainAsync(caller, recipient);
    }

    [Test]
    public async Task LargeRpc_Repeated20MiBRoundTrips_DoNotAccumulateHubQueueBytes()
    {
        var callerId = NextClientId("phase4_repeated_caller");
        var recipientId = NextClientId("phase4_repeated_recipient");
        await using var caller = CreateClient(callerId);
        await using var recipient = CreateClient(recipientId);

        recipient.RegisterHandler("echo-repeated-large", static (payload, _) =>
            Task.FromResult((HttpStatusCode.OK, payload)));

        await caller.ConnectAsync();
        await recipient.ConnectAsync();

        var payload = GC.AllocateUninitializedArray<byte>(20 * MiB);
        payload.AsSpan().Fill(20);

        for (var iteration = 0; iteration < 3; iteration++)
        {
            var (statusCode, response) = await caller
                .InvokeAsync(recipientId, "echo-repeated-large", payload)
                .WaitAsync(TimeSpan.FromSeconds(120));

            statusCode.Should().Be(HttpStatusCode.OK);
            response.Span.SequenceEqual(payload).Should().BeTrue();
        }

        await WaitForLargeRpcBuffersToDrainAsync(caller, recipient);
    }

    [Test]
    public async Task LargeRpc_RequestAbove32MiB_Returns413WithoutInvokingHandler()
    {
        var callerId = NextClientId("phase4_oversized_caller");
        var recipientId = NextClientId("phase4_oversized_recipient");
        await using var caller = CreateClient(callerId);
        await using var recipient = CreateClient(recipientId);
        var handlerInvocations = 0;

        recipient.RegisterHandler("reject-oversized", (_, _) =>
        {
            Interlocked.Increment(ref handlerInvocations);
            return Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty));
        });

        await caller.ConnectAsync();
        await recipient.ConnectAsync();

        var payload = GC.AllocateUninitializedArray<byte>(MaxLargeRpcPayloadBytes + 1);
        var (statusCode, response) = await caller.InvokeAsync(recipientId, "reject-oversized", payload);

        statusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        response.Length.Should().Be(0);
        Volatile.Read(ref handlerInvocations).Should().Be(0);
        GetBufferedLargeRpcBytes(caller).Should().Be(0);
        GetBufferedLargeRpcBytes(recipient).Should().Be(0);
    }

    [Test]
    public async Task LargeRpc_CancellationDuringTransfer_CompletesAndReleasesBuffers()
    {
        var callerId = NextClientId("phase4_cancel_caller");
        var recipientId = NextClientId("phase4_cancel_recipient");
        await using var caller = CreateClient(callerId, streamChunkSize: 1024);
        await using var recipient = CreateClient(recipientId, streamChunkSize: 1024);

        recipient.RegisterHandler(
            "cancel-transfer",
            static async (ReadOnlyMemory<byte> _, Guid _, CancellationToken ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return (HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty);
        });
        recipient.RegisterHandler("after-cancel", static (_, _) =>
            Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty)));

        await caller.ConnectAsync();
        await recipient.ConnectAsync();

        using var cts = new CancellationTokenSource();
        var payload = GC.AllocateUninitializedArray<byte>(MaxLargeRpcPayloadBytes);
        var invoke = caller.InvokeAsync(recipientId, "cancel-transfer", payload, cts.Token);

        await WaitForConditionAsync(
            () => GetBufferedLargeRpcBytes(recipient) > 0,
            TimeSpan.FromSeconds(5),
            "the recipient did not reserve its large-RPC buffer");
        cts.Cancel();

        await FluentActions.Awaiting(() => invoke.WaitAsync(TimeSpan.FromSeconds(10)))
            .Should().ThrowAsync<OperationCanceledException>();
        await WaitForLargeRpcBuffersToDrainAsync(caller, recipient);

        var (statusCode, _) = await caller
            .InvokeAsync(recipientId, "after-cancel", new byte[] { 1 })
            .WaitAsync(TimeSpan.FromSeconds(5));
        statusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task LargeRpc_RecipientDisconnectDuringTransfer_CompletesWithoutHanging()
    {
        var callerId = NextClientId("phase4_disconnect_caller");
        var recipientId = NextClientId("phase4_disconnect_recipient");
        await using var caller = CreateClient(callerId, streamChunkSize: 1024);
        var recipient = CreateClient(recipientId, streamChunkSize: 1024);

        try
        {
            recipient.RegisterHandler("disconnect-transfer", static (payload, _) =>
                Task.FromResult((HttpStatusCode.OK, payload)));

            await caller.ConnectAsync();
            await recipient.ConnectAsync();

            var payload = GC.AllocateUninitializedArray<byte>(MaxLargeRpcPayloadBytes);
            var invoke = caller.InvokeAsync(recipientId, "disconnect-transfer", payload);

            await WaitForConditionAsync(
                () => GetBufferedLargeRpcBytes(recipient) > 0,
                TimeSpan.FromSeconds(5),
                "the recipient did not begin the large-RPC transfer");
            await recipient.DisposeAsync();

            var completed = await Task.WhenAny(invoke, Task.Delay(TimeSpan.FromSeconds(10)));
            completed.Should().BeSameAs(invoke, "disconnect must terminate an in-flight large RPC");

            if (invoke.IsCompletedSuccessfully)
            {
                var (statusCode, _) = await invoke;
                statusCode.Should().NotBe(HttpStatusCode.OK);
            }
            else
            {
                var exception = await RecordExceptionAsync(invoke);
                exception.Should().NotBeNull();
                exception.Should().NotBeOfType<TimeoutException>();
            }

            await WaitForLargeRpcBuffersToDrainAsync(caller, recipient);
        }
        finally
        {
            await recipient.DisposeAsync();
        }
    }

    private BoltClient CreateClient(string clientId, int streamChunkSize = 256 * 1024) =>
        new(
            new Uri($"ws://localhost:{_port}/bolt"),
            clientId,
            clientId,
            new BoltClientOptions
            {
                RpcTimeoutSeconds = 120,
                LargePayloadThreshold = 64 * 1024,
                MaxLargeRpcPayloadBytes = MaxLargeRpcPayloadBytes,
                MaxBufferedLargeRpcBytes = 64L * MiB,
                StreamChunkSize = streamChunkSize,
                MaxConnections = 1
            },
            _loggerFactory.CreateLogger<BoltClient>());

    private string NextClientId(string prefix) =>
        $"{prefix}_{Interlocked.Increment(ref _clientSequence)}";

    private static long GetBufferedLargeRpcBytes(BoltClient client) =>
        (long)(typeof(BoltClient)
            .GetField("_bufferedLargeRpcBytes", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(client) ?? throw new InvalidOperationException("BoltClient large-RPC buffer counter not found."));

    private static int GetPrivateInt(BoltClient client, string fieldName) =>
        (int)(typeof(BoltClient)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(client) ?? throw new InvalidOperationException($"BoltClient field '{fieldName}' not found."));

    private static async Task WaitForLargeRpcBuffersToDrainAsync(params BoltClient[] clients) =>
        await WaitForConditionAsync(
            () => clients.All(client => GetBufferedLargeRpcBytes(client) == 0),
            TimeSpan.FromSeconds(10),
            "large-RPC buffer reservations were not released");

    private static async Task WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string timeoutMessage)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        throw new TimeoutException(timeoutMessage);
    }

    private static async Task<Exception?> RecordExceptionAsync(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task WaitForHealthAsync(string url)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync(url)).IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // The in-process server may still be binding its listener.
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Service at {url} was not healthy within 15 seconds.");
    }
}
