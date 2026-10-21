using System.Net;
using Bolt.Client;
using Bolt.Protocol;
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
[CancelAfter(30_000)]
public sealed class BoltInboundRequestContextTests
{
    private WebApplication _serverApp = null!;
    private ILoggerFactory _loggerFactory = null!;
    private Uri _serverUri = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBoltServer();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _serverApp = builder.Build();
        _serverApp.UseWebSockets();
        _serverApp.MapBolt("/bolt");
        await _serverApp.StartAsync();
        _serverUri = new UriBuilder(_serverApp.Urls.Single())
        {
            Scheme = "ws",
            Path = "/bolt"
        }.Uri;

        _loggerFactory = _serverApp.Services.GetRequiredService<ILoggerFactory>();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        try { await _serverApp.StopAsync(); } catch { }
        try { await _serverApp.DisposeAsync(); } catch { }
    }

    [Test]
    public async Task ContextAwareHandler_ReceivesSenderAndRequestId_ForUnaryAndLargeRpc()
    {
        const string callerId = "context_caller";
        const string recipientId = "context_recipient";
        await using var caller = CreateClient(callerId);
        await using var recipient = CreateClient(recipientId);
        var contexts = new List<BoltInboundRequestContext>();

        recipient.RegisterHandler(
            "capture-context",
            (ReadOnlyMemory<byte> payload, BoltInboundRequestContext context, CancellationToken _) =>
            {
                lock (contexts)
                    contexts.Add(context);
                return Task.FromResult((HttpStatusCode.OK, ReadOnlyMemory<byte>.Empty));
            });

        await caller.ConnectAsync();
        await recipient.ConnectAsync();

        (await caller.InvokeAsync(recipientId, "capture-context", new byte[32]))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await caller.InvokeAsync(recipientId, "capture-context", new byte[2048]))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        contexts.Should().HaveCount(2);
        contexts.Should().OnlyContain(context => context.RequestId != Guid.Empty);
        contexts.Select(context => context.RequestId).Should().OnlyHaveUniqueItems();
        contexts.Should().OnlyContain(context => context.SenderHash == BoltCodec.Fnv1aHash(callerId));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void PushAsync_BlankRecipient_IsRejectedByRawAndTypedOverloads(string? recipientId)
    {
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        var client = new BoltClient(
            new Uri("ws://localhost/bolt"),
            "push_sender",
            "push_sender",
            new BoltClientOptions(),
            loggerFactory.CreateLogger<BoltClient>());

        Assert.That(
            async () => await client.PushAsync(recipientId!, "raw", ReadOnlyMemory<byte>.Empty),
            Throws.InstanceOf<ArgumentException>());
        Assert.That(
            async () => await client.PushAsync(recipientId!, "typed", 42),
            Throws.InstanceOf<ArgumentException>());
    }

    private BoltClient CreateClient(string clientId) =>
        new(
            _serverUri,
            clientId,
            clientId,
            new BoltClientOptions
            {
                RpcTimeoutSeconds = 10,
                LargePayloadThreshold = 1024,
                StreamChunkSize = 1024,
                MaxConnections = 1
            },
            _loggerFactory.CreateLogger<BoltClient>());
}
