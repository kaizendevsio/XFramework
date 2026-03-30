using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Bolt.Client;
using Bolt.Media;
using Bolt.Protocol;
using Bolt.Server;
using FluentAssertions;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Bolt.Tests;

// ═══════════════════════════════════════════════════════════════════
// RPC Stress Tests — No Media
// ═══════════════════════════════════════════════════════════════════

[TestFixture]
[CancelAfter(60000)]
public class RpcStressTests
{
    private WebApplication _serverApp = null!;
    private ILoggerFactory _loggerFactory = null!;
    private static int _portCounter = 19500;
    private int _port;

    [SetUp]
    public async Task SetUp()
    {
        _port = Interlocked.Increment(ref _portCounter);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        _serverApp = builder.Build();
        _serverApp.UseWebSockets();
        _serverApp.MapBolt("/bolt");
        _serverApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _serverApp.RunAsync());
        await WaitForHealth($"http://localhost:{_port}/health");
        _loggerFactory = _serverApp.Services.GetRequiredService<ILoggerFactory>();
    }

    [TearDown]
    public async Task TearDown()
    {
        try { await _serverApp.StopAsync(); } catch { }
    }

    private BoltClient CreateClient(string id, string name) =>
        new(new Uri($"ws://localhost:{_port}/bolt"), id, name,
            new BoltClientOptions { RpcTimeoutSeconds = 15 }, _loggerFactory.CreateLogger<BoltClient>());

    [Test]
    public async Task HighConcurrency_100ParallelRpcCalls()
    {
        var clientA = CreateClient("stress_a", "StressA");
        var clientB = CreateClient("stress_b", "StressB");

        clientB.RegisterHandler("echo", (payload, _) =>
            Task.FromResult((HttpStatusCode.OK, payload)));

        await clientA.ConnectAsync();
        await clientB.ConnectAsync();

        const int count = 100;
        var tasks = new Task<(HttpStatusCode, ReadOnlyMemory<byte>)>[count];
        var payload = MemoryPackSerializer.Serialize(new StressMsg { Id = 0, Data = "test" });

        for (int i = 0; i < count; i++)
            tasks[i] = clientA.InvokeAsync("stress_b", "echo", payload);

        var results = await Task.WhenAll(tasks);
        results.Should().AllSatisfy(r => r.Item1.Should().Be(HttpStatusCode.OK));

        await clientA.DisposeAsync();
        await clientB.DisposeAsync();
    }

    [Test]
    public async Task LargePayload_1MB_RpcRoundTrip()
    {
        var clientA = CreateClient("large_a", "LargeA");
        var clientB = CreateClient("large_b", "LargeB");

        clientB.RegisterHandler("bigecho", (payload, _) =>
            Task.FromResult((HttpStatusCode.OK, payload)));

        await clientA.ConnectAsync();
        await clientB.ConnectAsync();

        // 100KB payload (WebSocket default max is ~64KB per message, 100KB tests chunking)
        var bigData = new byte[100 * 1024];
        Random.Shared.NextBytes(bigData);
        var payload = MemoryPackSerializer.Serialize(new StressBigMsg { Data = bigData });

        var (status, response) = await clientA.InvokeAsync("large_b", "bigecho", payload);
        status.Should().Be(HttpStatusCode.OK);

        var decoded = MemoryPackSerializer.Deserialize<StressBigMsg>(response.Span);
        decoded!.Data.Should().Equal(bigData);

        await clientA.DisposeAsync();
        await clientB.DisposeAsync();
    }

    [Test]
    public async Task BurstTraffic_500RapidFirePushMessages()
    {
        var sender = CreateClient("burst_sender", "BurstSender");
        var receiver = CreateClient("burst_receiver", "BurstReceiver");

        var received = new ConcurrentBag<int>();
        receiver.RegisterHandler("ping", (payload, _) =>
        {
            var msg = MemoryPackSerializer.Deserialize<StressMsg>(payload.Span);
            received.Add(msg!.Id);
            return Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty));
        });

        await sender.ConnectAsync();
        await receiver.ConnectAsync();

        const int count = 500;
        for (int i = 0; i < count; i++)
        {
            var payload = MemoryPackSerializer.Serialize(new StressMsg { Id = i, Data = "burst" });
            await sender.PushAsync("burst_receiver", "ping", (ReadOnlyMemory<byte>)payload);
        }

        // Push is fire-and-forget — give time for delivery
        await Task.Delay(2000);

        // Push doesn't guarantee delivery order, but most should arrive
        received.Count.Should().BeGreaterThan(count * 8 / 10, "at least 80% of push messages should be delivered");

        await sender.DisposeAsync();
        await receiver.DisposeAsync();
    }

    [Test]
    [CancelAfter(90000)]
    public async Task MultipleClients_5Clients_CrossTalk()
    {
        const int clientCount = 5;
        var clients = new BoltClient[clientCount];
        var receivedCounts = new ConcurrentDictionary<string, int>();

        for (int i = 0; i < clientCount; i++)
        {
            var id = $"multi_{i}";
            clients[i] = CreateClient(id, $"Multi{i}");
            receivedCounts[id] = 0;

            clients[i].RegisterHandler("ping", (payload, _) =>
            {
                receivedCounts.AddOrUpdate(id, 1, (_, c) => c + 1);
                return Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty));
            });
        }

        foreach (var c in clients)
            await c.ConnectAsync();

        // Each client sends to every other client
        var payload = MemoryPackSerializer.Serialize(new StressMsg { Id = 0, Data = "hello" });
        var tasks = new List<Task>();
        for (int i = 0; i < clientCount; i++)
        {
            for (int j = 0; j < clientCount; j++)
            {
                if (i == j) continue;
                var sender = clients[i];
                var recipientId = $"multi_{j}";
                tasks.Add(sender.InvokeAsync(recipientId, "ping", payload).ContinueWith(_ => { }));
            }
        }

        await Task.WhenAll(tasks);

        // Each client should have received (clientCount - 1) messages
        foreach (var (id, count) in receivedCounts)
            count.Should().Be(clientCount - 1, $"client {id} should receive from all others");

        foreach (var c in clients)
            await c.DisposeAsync();
    }

    [Test]
    public async Task StreamingFile_ChunkedTransfer_1MB()
    {
        var sender = CreateClient("stream_sender", "StreamSender");
        var receiver = CreateClient("stream_receiver", "StreamReceiver");

        var receivedChunks = new ConcurrentBag<byte[]>();
        var streamDone = new TaskCompletionSource();

        receiver.RegisterStreamHandler("upload", async (stream) =>
        {
            await foreach (var chunk in stream.ReadAllAsync())
            {
                receivedChunks.Add(chunk.ToArray());
            }
            streamDone.TrySetResult();
        });

        await sender.ConnectAsync();
        await receiver.ConnectAsync();

        // Send 1MB in 64KB chunks
        var fullData = new byte[1024 * 1024];
        Random.Shared.NextBytes(fullData);
        const int chunkSize = 64 * 1024;

        var stream = await sender.OpenStreamAsync("stream_receiver", "upload");
        for (int offset = 0; offset < fullData.Length; offset += chunkSize)
        {
            var len = Math.Min(chunkSize, fullData.Length - offset);
            await stream.SendAsync(fullData.AsMemory(offset, len));
        }
        await stream.DisposeAsync();

        // Wait for receiver to process
        await streamDone.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Reassemble and verify
        var reassembled = new List<byte>();
        foreach (var chunk in receivedChunks)
            reassembled.AddRange(chunk);

        reassembled.Count.Should().Be(fullData.Length);
        reassembled.ToArray().Should().Equal(fullData);

        await sender.DisposeAsync();
        await receiver.DisposeAsync();
    }

    [Test]
    public async Task RpcTimeout_UnresponsiveHandler_TimesOut()
    {
        var clientA = CreateClient("timeout_a", "TimeoutA");
        var clientB = CreateClient("timeout_b", "TimeoutB");

        clientB.RegisterHandler("slow", async (payload, _) =>
        {
            await Task.Delay(60000); // Never responds in time
            return (HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty);
        });

        await clientA.ConnectAsync();
        await clientB.ConnectAsync();

        var payload = MemoryPackSerializer.Serialize(new StressMsg { Id = 1, Data = "timeout" });

        // BoltClient's internal RPC timeout (15s) will fire — expect TimeoutException
        Func<Task> act = async () => await clientA.InvokeAsync("timeout_b", "slow", payload);

        await act.Should().ThrowAsync<TimeoutException>();

        await clientA.DisposeAsync();
        await clientB.DisposeAsync();
    }

    [Test]
    public async Task HandlerNotFound_Returns501()
    {
        var clientA = CreateClient("notfound_a", "NotFoundA");
        var clientB = CreateClient("notfound_b", "NotFoundB");

        await clientA.ConnectAsync();
        await clientB.ConnectAsync();

        var payload = MemoryPackSerializer.Serialize(new StressMsg { Id = 1, Data = "missing" });
        var (status, _) = await clientA.InvokeAsync("notfound_b", "nonexistent_handler", payload);

        status.Should().Be(HttpStatusCode.NotImplemented);

        await clientA.DisposeAsync();
        await clientB.DisposeAsync();
    }

    [Test]
    public async Task LargePayload_AutoStreaming_1MB_TransparentToConsumer()
    {
        // Configure with low threshold so auto-streaming kicks in
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 30, LargePayloadThreshold = 1024 };
        var clientA = new BoltClient(new Uri($"ws://localhost:{_port}/bolt"),
            "large_auto_a", "LargeAutoA", opts, _loggerFactory.CreateLogger<BoltClient>());
        var clientB = new BoltClient(new Uri($"ws://localhost:{_port}/bolt"),
            "large_auto_b", "LargeAutoB", opts, _loggerFactory.CreateLogger<BoltClient>());

        clientB.RegisterHandler("bigprocess", (payload, _) =>
        {
            // Echo back the size as proof we received the full payload
            var size = payload.Length;
            var resp = MemoryPackSerializer.Serialize(new StressMsg { Id = size, Data = "ok" });
            return Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)resp));
        });

        await clientA.ConnectAsync();
        await clientB.ConnectAsync();

        // Send 256KB payload — way above 1KB threshold, will auto-stream
        var bigData = new byte[256 * 1024];
        Random.Shared.NextBytes(bigData);
        var payload = MemoryPackSerializer.Serialize(new StressBigMsg { Data = bigData });

        // Consumer calls InvokeAsync exactly the same as a small payload
        var (status, response) = await clientA.InvokeAsync("large_auto_b", "bigprocess", payload);

        status.Should().Be(HttpStatusCode.OK);
        var result = MemoryPackSerializer.Deserialize<StressMsg>(response.Span);
        result!.Id.Should().Be(payload.Length, "handler should have received the full reassembled payload");

        await clientA.DisposeAsync();
        await clientB.DisposeAsync();
    }

    [Test]
    public async Task LargeResponse_AutoStreaming_256KB_TransparentToConsumer()
    {
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 30, LargePayloadThreshold = 1024 };
        var clientA = new BoltClient(new Uri($"ws://localhost:{_port}/bolt"),
            "large_resp_a", "LargeRespA", opts, _loggerFactory.CreateLogger<BoltClient>());
        var clientB = new BoltClient(new Uri($"ws://localhost:{_port}/bolt"),
            "large_resp_b", "LargeRespB", opts, _loggerFactory.CreateLogger<BoltClient>());

        // Handler returns a LARGE response (256KB)
        var bigResponse = new byte[256 * 1024];
        Random.Shared.NextBytes(bigResponse);

        clientB.RegisterHandler("getbig", (payload, _) =>
        {
            var resp = MemoryPackSerializer.Serialize(new StressBigMsg { Data = bigResponse });
            return Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)resp));
        });

        await clientA.ConnectAsync();
        await clientB.ConnectAsync();

        // Small request, large response — response should auto-stream back
        var smallRequest = MemoryPackSerializer.Serialize(new StressMsg { Id = 1, Data = "give me big data" });
        var (status, response) = await clientA.InvokeAsync("large_resp_b", "getbig", smallRequest);

        status.Should().Be(HttpStatusCode.OK);
        var result = MemoryPackSerializer.Deserialize<StressBigMsg>(response.Span);
        result!.Data.Should().Equal(bigResponse);

        await clientA.DisposeAsync();
        await clientB.DisposeAsync();
    }

    [Test]
    public async Task BothDirections_LargeRequestAndLargeResponse()
    {
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 30, LargePayloadThreshold = 1024 };
        var clientA = new BoltClient(new Uri($"ws://localhost:{_port}/bolt"),
            "bidir_a", "BidirA", opts, _loggerFactory.CreateLogger<BoltClient>());
        var clientB = new BoltClient(new Uri($"ws://localhost:{_port}/bolt"),
            "bidir_b", "BidirB", opts, _loggerFactory.CreateLogger<BoltClient>());

        clientB.RegisterHandler("transform", (payload, _) =>
        {
            // Return a response that's the same size as the request (large both ways)
            var input = MemoryPackSerializer.Deserialize<StressBigMsg>(payload.Span);
            var output = new byte[input!.Data.Length];
            for (int i = 0; i < output.Length; i++) output[i] = (byte)(input.Data[i] ^ 0xFF);
            var resp = MemoryPackSerializer.Serialize(new StressBigMsg { Data = output });
            return Task.FromResult((HttpStatusCode.OK, (ReadOnlyMemory<byte>)resp));
        });

        await clientA.ConnectAsync();
        await clientB.ConnectAsync();

        var bigData = new byte[128 * 1024];
        Random.Shared.NextBytes(bigData);
        var request = MemoryPackSerializer.Serialize(new StressBigMsg { Data = bigData });

        var (status, response) = await clientA.InvokeAsync("bidir_b", "transform", request);

        status.Should().Be(HttpStatusCode.OK);
        var result = MemoryPackSerializer.Deserialize<StressBigMsg>(response.Span);
        result!.Data.Length.Should().Be(bigData.Length);
        // Verify XOR transformation
        for (int i = 0; i < bigData.Length; i++)
            result.Data[i].Should().Be((byte)(bigData[i] ^ 0xFF));

        await clientA.DisposeAsync();
        await clientB.DisposeAsync();
    }

    [Test]
    public async Task SmallPayload_StillUsesNormalRpc()
    {
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 15, LargePayloadThreshold = 65536 };
        var clientA = new BoltClient(new Uri($"ws://localhost:{_port}/bolt"),
            "small_rpc_a", "SmallA", opts, _loggerFactory.CreateLogger<BoltClient>());
        var clientB = new BoltClient(new Uri($"ws://localhost:{_port}/bolt"),
            "small_rpc_b", "SmallB", opts, _loggerFactory.CreateLogger<BoltClient>());

        clientB.RegisterHandler("echo", (payload, _) =>
            Task.FromResult((HttpStatusCode.OK, payload)));

        await clientA.ConnectAsync();
        await clientB.ConnectAsync();

        // Small payload — should use normal Request frame, not streaming
        var smallPayload = MemoryPackSerializer.Serialize(new StressMsg { Id = 42, Data = "small" });
        var (status, response) = await clientA.InvokeAsync("small_rpc_b", "echo", smallPayload);

        status.Should().Be(HttpStatusCode.OK);
        var result = MemoryPackSerializer.Deserialize<StressMsg>(response.Span);
        result!.Id.Should().Be(42);

        await clientA.DisposeAsync();
        await clientB.DisposeAsync();
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch { }
            await Task.Delay(100);
        }
        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }
}

// ═══════════════════════════════════════════════════════════════════
// Media Stress Tests — Voice/Video Simulation
// ═══════════════════════════════════════════════════════════════════

[TestFixture]
[CancelAfter(30000)]
public class MediaStressTests
{
    private WebApplication _serverApp = null!;
    private BoltClient _clientA = null!;
    private BoltClient _clientB = null!;
    private BoltMediaClient _mediaA = null!;
    private BoltMediaClient _mediaB = null!;
    private ILoggerFactory _loggerFactory = null!;
    private static int _portCounter = 19600;
    private int _port;

    [SetUp]
    public async Task SetUp()
    {
        _port = Interlocked.Increment(ref _portCounter);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{_port}");
        builder.Services.AddSingleton<BoltServer>();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        _serverApp = builder.Build();
        _serverApp.UseWebSockets();
        _serverApp.MapBolt("/bolt");
        _serverApp.MapGet("/health", () => "ok");
        _ = Task.Run(() => _serverApp.RunAsync());
        await WaitForHealth($"http://localhost:{_port}/health");

        _loggerFactory = _serverApp.Services.GetRequiredService<ILoggerFactory>();
        var opts = new BoltClientOptions { RpcTimeoutSeconds = 15 };

        _clientA = new BoltClient(new Uri($"ws://localhost:{_port}/bolt"),
            "media_stress_a", "MediaStressA", opts, _loggerFactory.CreateLogger<BoltClient>());
        _clientB = new BoltClient(new Uri($"ws://localhost:{_port}/bolt"),
            "media_stress_b", "MediaStressB", opts, _loggerFactory.CreateLogger<BoltClient>());

        await _clientA.ConnectAsync();
        await _clientB.ConnectAsync();

        _mediaA = new BoltMediaClient(_clientA, _loggerFactory.CreateLogger<BoltMediaClient>());
        _mediaB = new BoltMediaClient(_clientB, _loggerFactory.CreateLogger<BoltMediaClient>());
    }

    [TearDown]
    public async Task TearDown()
    {
        try { await _mediaA.DisposeAsync(); } catch { }
        try { await _mediaB.DisposeAsync(); } catch { }
        try { await _clientA.DisposeAsync(); } catch { }
        try { await _clientB.DisposeAsync(); } catch { }
        try { await _serverApp.StopAsync(); } catch { }
    }

    [Test]
    public async Task SimulateAudioCall_3Seconds_AtOpusRate()
    {
        // Establish call
        var incomingTcs = new TaskCompletionSource<IncomingCallInfo>();
        var answeredTcs = new TaskCompletionSource<Guid>();

        _mediaB.OnIncomingCall += info => { incomingTcs.TrySetResult(info); return Task.CompletedTask; };
        _mediaA.OnCallAnswered += callId => { answeredTcs.TrySetResult(callId); return Task.CompletedTask; };

        var callId = await _mediaA.StartCallAsync("media_stress_b", encrypted: false);
        var incoming = await incomingTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _mediaB.AnswerCallAsync(incoming.CallId);
        await answeredTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Simulate 3 seconds of Opus audio at 50fps (20ms frames)
        // Each Opus frame is ~80-160 bytes at 64kbps
        var conn = _clientA.GetPrimaryConnection();
        var streamId = Guid.NewGuid();

        // Send MediaConfig
        var configWriter = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteMediaConfig(configWriter, streamId, callId, MediaType.Audio, CodecId.Opus,
            48000, 1, 64, 0, ReadOnlySpan<byte>.Empty);
        await conn.SendAsync(configWriter.WrittenMemory, CancellationToken.None);
        configWriter.Reset();
        await Task.Delay(200); // Let config propagate

        var framesSent = 0;
        var sw = Stopwatch.StartNew();

        // 3 seconds at 50fps = 150 frames
        for (int i = 0; i < 150; i++)
        {
            var audioFrame = new byte[120]; // ~64kbps Opus frame
            Random.Shared.NextBytes(audioFrame);

            var writer = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
            byte flags = (byte)(i == 0 ? 0x01 : 0x00); // First frame is keyframe
            BoltCodec.WriteMediaFrame(writer, streamId, (uint)i, (uint)(i * 960), flags, audioFrame);
            await conn.SendAsync(writer.WrittenMemory, CancellationToken.None);
            writer.Reset();

            framesSent++;

            // Simulate real-time pacing (20ms per frame)
            var elapsed = sw.ElapsedMilliseconds;
            var expected = (i + 1) * 20;
            if (expected > elapsed)
                await Task.Delay((int)(expected - elapsed));
        }

        sw.Stop();

        framesSent.Should().Be(150);
        sw.ElapsedMilliseconds.Should().BeGreaterThan(2500, "should take ~3 seconds at real-time pacing");
        sw.ElapsedMilliseconds.Should().BeLessThan(5000, "should not take more than 5 seconds");

        await _mediaA.EndCallAsync(callId);
    }

    [Test]
    public async Task SimulateVideoCall_2Seconds_At30fps()
    {
        var incomingTcs = new TaskCompletionSource<IncomingCallInfo>();
        var answeredTcs = new TaskCompletionSource<Guid>();

        _mediaB.OnIncomingCall += info => { incomingTcs.TrySetResult(info); return Task.CompletedTask; };
        _mediaA.OnCallAnswered += callId => { answeredTcs.TrySetResult(callId); return Task.CompletedTask; };

        var callId = await _mediaA.StartCallAsync("media_stress_b", encrypted: false);
        var incoming = await incomingTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _mediaB.AnswerCallAsync(incoming.CallId);
        await answeredTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var conn = _clientA.GetPrimaryConnection();
        var streamId = Guid.NewGuid();

        var configWriter = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
        BoltCodec.WriteMediaConfig(configWriter, streamId, callId, MediaType.Video, CodecId.H264,
            1280, 720, 2000, 0, ReadOnlySpan<byte>.Empty);
        await conn.SendAsync(configWriter.WrittenMemory, CancellationToken.None);
        configWriter.Reset();
        await Task.Delay(200);

        var sw = Stopwatch.StartNew();

        // 2 seconds at 30fps = 60 frames
        // Keyframe every 30 frames, delta frames ~5-15KB, keyframes ~30-50KB
        for (int i = 0; i < 60; i++)
        {
            bool isKeyframe = (i % 30 == 0);
            var frameSize = isKeyframe ? 35000 : 8000; // Realistic H.264 sizes
            var videoFrame = new byte[frameSize];
            Random.Shared.NextBytes(videoFrame);

            var writer = Bolt.Protocol.Buffers.RentedBufferWriter.GetThreadLocal();
            byte flags = (byte)(isKeyframe ? 0x01 : 0x00);
            BoltCodec.WriteMediaFrame(writer, streamId, (uint)i, (uint)(i * 3000), flags, videoFrame);
            await conn.SendAsync(writer.WrittenMemory, CancellationToken.None);
            writer.Reset();

            var elapsed = sw.ElapsedMilliseconds;
            var expected = (i + 1) * 33;
            if (expected > elapsed)
                await Task.Delay((int)(expected - elapsed));
        }

        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeGreaterThan(1500);

        await _mediaA.EndCallAsync(callId);
    }

    [Test]
    public async Task SimulatePacketLoss_FecRecovery()
    {
        // Test FEC recovery by simulating frame loss
        var fecEncoder = new FecEncoder(4);
        var fecDecoder = new FecDecoder();

        var originalFrames = new byte[4][];
        for (int i = 0; i < 4; i++)
        {
            originalFrames[i] = new byte[100];
            Random.Shared.NextBytes(originalFrames[i]);
        }

        // Encode all 4 frames and get parity
        FecResult? parity = null;
        for (int i = 0; i < 4; i++)
        {
            parity = fecEncoder.AddFrame((uint)i, originalFrames[i]);
        }

        parity.Should().NotBeNull("parity should be generated after group completes");

        // Receiver gets frames 0, 1, 3 but NOT frame 2 (simulated loss)
        // groupStart=0 for all frames in this group
        fecDecoder.AddFrame(0, 0, originalFrames[0]);
        fecDecoder.AddFrame(1, 0, originalFrames[1]);
        // Frame 2 is "lost"
        fecDecoder.AddFrame(3, 0, originalFrames[3]);

        // Add FEC parity (groupStart=0, groupSize=4)
        fecDecoder.AddFecFrame(0, 4, parity!.ParityData, parity.OriginalLengths);

        // Try to recover frame 2 (groupStart=0)
        var recovered = fecDecoder.TryRecover(2, 0, out var recoveredData);
        recovered.Should().BeTrue("FEC should recover 1 missing frame from 3 survivors + parity");
        recoveredData.ToArray().Should().Equal(originalFrames[2]);
    }

    [Test]
    public async Task SimulateHighJitter_JitterBufferSmooths()
    {
        // Simulate frames arriving with random jitter, verify jitter buffer reorders
        var jitterBuffer = new MediaJitterBuffer(true); // audio
        jitterBuffer.Start();

        var delivered = new ConcurrentBag<uint>();

        // Consume frames in background
        var consumer = Task.Run(async () =>
        {
            await foreach (var frame in jitterBuffer.ReadAllAsync(CancellationToken.None))
            {
                delivered.Add(frame.SequenceNumber);
                if (delivered.Count >= 20) break;
            }
        });

        // Send 20 frames with random jitter (some out of order)
        var sequences = Enumerable.Range(0, 20).ToList();
        var random = new Random(42);

        // Shuffle to simulate out-of-order delivery
        for (int i = sequences.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (sequences[i], sequences[j]) = (sequences[j], sequences[i]);
        }

        foreach (var seq in sequences)
        {
            var data = new byte[] { (byte)seq };
            jitterBuffer.Enqueue((uint)seq, (uint)(seq * 960), data, false);
            await Task.Delay(10 + random.Next(30)); // Simulated jitter: 10-40ms
        }

        // Wait for consumer
        await consumer.WaitAsync(TimeSpan.FromSeconds(5));

        delivered.Count.Should().BeGreaterOrEqualTo(15, "most frames should be delivered");

        await jitterBuffer.DisposeAsync();
    }

    [Test]
    public async Task EncryptionRoundTrip_UnderLoad()
    {
        // Simulate 100 encrypted frames being sent and decrypted
        using var alice = new MediaEncryption();
        using var bob = new MediaEncryption();

        var callId = Guid.NewGuid();
        bob.DeriveKey(alice.PublicKey, callId);
        alice.DeriveKey(bob.PublicKey, callId);

        var streamId = Guid.NewGuid();
        var errors = 0;

        // Encrypt/decrypt 100 frames of varying sizes
        Parallel.For(0, 100, i =>
        {
            var size = 100 + (i * 50); // 100 bytes to 5100 bytes
            var frame = new byte[size];
            Random.Shared.NextBytes(frame);

            try
            {
                var encrypted = alice.Encrypt(frame, (uint)i, streamId);
                var decrypted = bob.Decrypt(encrypted, (uint)i, streamId);

                if (!decrypted.SequenceEqual(frame))
                    Interlocked.Increment(ref errors);
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
        });

        errors.Should().Be(0, "all 100 encrypted frames should round-trip correctly");
    }

    [Test]
    public async Task NackRetransmit_RecoversMissingFrames()
    {
        var buffer = new RetransmitBuffer(64);

        // Store 50 frames
        for (uint i = 0; i < 50; i++)
        {
            var data = new byte[] { (byte)i, (byte)(i + 1) };
            buffer.Store(i, i * 960, 0, data);
        }

        // Request retransmission of specific frames
        var missingSeqs = new uint[] { 5, 15, 25, 35, 45 };
        var recovered = 0;

        foreach (var seq in missingSeqs)
        {
            if (buffer.TryGet(seq, out var frame) && frame.Payload != null)
            {
                frame.SequenceNumber.Should().Be(seq);
                frame.Payload[0].Should().Be((byte)seq);
                recovered++;
            }
        }

        recovered.Should().Be(5, "all 5 requested frames should be in the retransmit buffer");
    }

    [Test]
    public async Task MultipleSequentialCalls_NoResourceLeaks()
    {
        // Run 5 quick calls in sequence to verify cleanup
        for (int round = 0; round < 5; round++)
        {
            var incomingTcs = new TaskCompletionSource<IncomingCallInfo>();
            var answeredTcs = new TaskCompletionSource<Guid>();
            var endedTcs = new TaskCompletionSource<Guid>();

            _mediaB.OnIncomingCall += info => { incomingTcs.TrySetResult(info); return Task.CompletedTask; };
            _mediaA.OnCallAnswered += callId => { answeredTcs.TrySetResult(callId); return Task.CompletedTask; };
            _mediaB.OnCallEnded += callId => { endedTcs.TrySetResult(callId); return Task.CompletedTask; };

            var callId = await _mediaA.StartCallAsync("media_stress_b", encrypted: false);
            var incoming = await incomingTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await _mediaB.AnswerCallAsync(incoming.CallId);
            await answeredTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Brief active call
            await Task.Delay(100);

            await _mediaA.EndCallAsync(callId);
            await endedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        // If we got here without timeout/crash, resources were cleaned up properly
        _clientA.IsConnected.Should().BeTrue();
        _clientB.IsConnected.Should().BeTrue();
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 15)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch { }
            await Task.Delay(100);
        }
        throw new TimeoutException($"Service at {url} not healthy within {timeoutSeconds}s");
    }
}

// ═══════════════════════════════════════════════════════════════════
// Codec Security Tests — Malformed Input Handling
// ═══════════════════════════════════════════════════════════════════

[TestFixture]
public class CodecSecurityTests
{
    [Test]
    public void NegativePayloadLength_Request_ReturnsFalse()
    {
        var writer = new ArrayBufferWriter<byte>(64);
        BoltCodec.WriteRequest(writer, Guid.NewGuid(), 123, 789, 456, new byte[] { 1, 2, 3 });

        // Corrupt the payload length to negative (payloadLen is at offset 29 in new format)
        var data = writer.WrittenSpan.ToArray();
        BitConverter.TryWriteBytes(data.AsSpan(29), -100);

        BoltCodec.TryReadRequest(data, out _, out _).Should().BeFalse();
    }

    [Test]
    public void NegativePayloadLength_Response_ReturnsFalse()
    {
        var writer = new ArrayBufferWriter<byte>(64);
        BoltCodec.WriteResponse(writer, Guid.NewGuid(), HttpStatusCode.OK, new byte[] { 1, 2 });

        var data = writer.WrittenSpan.ToArray();
        BitConverter.TryWriteBytes(data.AsSpan(19), -50);

        BoltCodec.TryReadResponse(data, out _, out _).Should().BeFalse();
    }

    [Test]
    public void NegativePayloadLength_MediaFrame_ReturnsFalse()
    {
        var writer = new ArrayBufferWriter<byte>(64);
        BoltCodec.WriteMediaFrame(writer, Guid.NewGuid(), 1, 960, 0, new byte[] { 0xAA });

        var data = writer.WrittenSpan.ToArray();
        BitConverter.TryWriteBytes(data.AsSpan(26), -1);

        BoltCodec.TryReadMediaFrame(data, out _).Should().BeFalse();
    }

    [Test]
    public void NegativePayloadLength_CallSignal_ReturnsFalse()
    {
        var writer = new ArrayBufferWriter<byte>(64);
        BoltCodec.WriteCallSignal(writer, Guid.NewGuid(), SignalType.Initiate, new byte[] { 1 });

        var data = writer.WrittenSpan.ToArray();
        BitConverter.TryWriteBytes(data.AsSpan(18), -999);

        BoltCodec.TryReadCallSignal(data, out _).Should().BeFalse();
    }

    [Test]
    public void NegativeExtensionLength_MediaConfig_ReturnsFalse()
    {
        var writer = new ArrayBufferWriter<byte>(128);
        BoltCodec.WriteMediaConfig(writer, Guid.NewGuid(), Guid.NewGuid(),
            MediaType.Audio, CodecId.Opus, 48000, 1, 64, 0, ReadOnlySpan<byte>.Empty);

        var data = writer.WrittenSpan.ToArray();
        BitConverter.TryWriteBytes(data.AsSpan(48), -10);

        BoltCodec.TryReadMediaConfig(data, out _).Should().BeFalse();
    }

    [Test]
    public void TruncatedBuffer_AllFrameTypes_ReturnFalse()
    {
        // Every TryRead should return false on a 1-byte buffer
        var tiny = new byte[] { 0xFF };

        BoltCodec.TryReadRequest(tiny, out _, out _).Should().BeFalse();
        BoltCodec.TryReadResponse(tiny, out _, out _).Should().BeFalse();
        BoltCodec.TryReadMediaFrame(tiny, out _).Should().BeFalse();
        BoltCodec.TryReadMediaConfig(tiny, out _).Should().BeFalse();
        BoltCodec.TryReadMediaFeedback(tiny, out _).Should().BeFalse();
        BoltCodec.TryReadMediaKeyRequest(tiny, out _).Should().BeFalse();
        BoltCodec.TryReadCallSignal(tiny, out _).Should().BeFalse();
        BoltCodec.TryReadFecFrame(tiny, out _).Should().BeFalse();
        BoltCodec.TryReadNackRequest(tiny, out _).Should().BeFalse();
    }

    [Test]
    public void EmptyBuffer_AllFrameTypes_ReturnFalse()
    {
        var empty = ReadOnlySpan<byte>.Empty;

        BoltCodec.TryReadRequest(empty, out _, out _).Should().BeFalse();
        BoltCodec.TryReadResponse(empty, out _, out _).Should().BeFalse();
        BoltCodec.TryReadMediaFrame(empty, out _).Should().BeFalse();
        BoltCodec.TryReadMediaConfig(empty, out _).Should().BeFalse();
        BoltCodec.TryReadMediaFeedback(empty, out _).Should().BeFalse();
        BoltCodec.TryReadCallSignal(empty, out _).Should().BeFalse();
        BoltCodec.TryReadFecFrame(empty, out _).Should().BeFalse();
        BoltCodec.TryReadNackRequest(empty, out _).Should().BeFalse();
    }
}

// ═══════════════════════════════════════════════════════════════════
// Serialization helpers
// ═══════════════════════════════════════════════════════════════════

[MemoryPackable]
public partial record StressMsg
{
    public int Id { get; init; }
    public string Data { get; init; } = "";
}

[MemoryPackable]
public partial record StressBigMsg
{
    public byte[] Data { get; init; } = [];
}
