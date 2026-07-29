using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bolt.Client;
using Bolt.Server;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XFramework.Bolt.Phase0Synthetics;
using XFramework.Domain.Shared.BusinessObjects;

namespace Bolt.Phase0Synthetics.Tests;

[CancelAfter(30000)]
[NonParallelizable]
public sealed class IdentityHealthCheckProbeIntegrationTests
{
    private static int _portCounter = 23100;

    [Test]
    public async Task InvokeAndValidateAsync_GeneratedSimpleRequestName_ReceivesValidResponse()
    {
        var port = Interlocked.Increment(ref _portCounter);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://localhost:{port}");
        builder.Services.AddBoltServer();
        var app = builder.Build();
        app.UseWebSockets();
        app.MapBolt("/bolt");
        app.MapGet("/health", () => "ok");
        var appTask = app.RunAsync();
        BoltClient? identityService = null;
        BoltClient? caller = null;

        try
        {
            await WaitForHealthAsync(port).WaitAsync(TimeSpan.FromSeconds(10));
            var target = new Uri($"ws://localhost:{port}/bolt");
            identityService = CreateClient(
                target,
                Sha256Hex("XFramework.IdentityServer"),
                "XFramework.IdentityServer");
            string? actorAccessToken = null;
            string? serviceAccessToken = null;
            identityService.RegisterHandler(
                nameof(HealthCheckRequest),
                (payload, _) =>
                {
                    var request = MemoryPackSerializer.Deserialize<HealthCheckRequest>(payload.Span);
                    actorAccessToken = request?.Metadata?.ActorAccessToken;
                    serviceAccessToken = request?.Metadata?.ServiceAccessToken;
                    var response = new QueryResponse<HealthCheckResponse>
                    {
                        HttpStatusCode = HttpStatusCode.OK,
                        Response = new HealthCheckResponse
                        {
                            Status = "ok",
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        }
                    };
                    return Task.FromResult<(HttpStatusCode, ReadOnlyMemory<byte>)>((
                        HttpStatusCode.OK,
                        MemoryPackSerializer.Serialize(response)));
                });
            await identityService.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));

            caller = CreateClient(target, "phase0-health-caller", "Phase0HealthCaller");
            await caller.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var options = CreateOptions(target);

            await IdentityHealthCheckProbe.InvokeAndValidateAsync(
                caller,
                options,
                options.PortalIdentityServiceToken,
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

            IdentityHealthCheckProbe.CommandName.Should().Be(nameof(HealthCheckRequest));
            actorAccessToken.Should().Be("user-actor-test-token");
            serviceAccessToken.Should().Be("portal-identity-service-test-token");
        }
        finally
        {
            if (caller is not null)
                await caller.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (identityService is not null)
                await identityService.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await app.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await app.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await appTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static BoltClient CreateClient(Uri target, string id, string name) =>
        new(
            target,
            id,
            name,
            new BoltClientOptions { RpcTimeoutSeconds = 5, TransportAttemptTimeoutMs = 5_000 },
            NullLogger<BoltClient>.Instance);

    private static SyntheticOptions CreateOptions(Uri target) =>
        new(
            target,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "integration-test",
            new SecretToken("communications-transport-test-token"),
            new SecretToken("communications-identity-service-test-token"),
            new SecretToken("portal-transport-test-token"),
            new SecretToken("portal-identity-service-test-token"),
            new SecretToken("user-actor-test-token"),
            TimeSpan.FromSeconds(5),
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            null,
            null);

    private static async Task WaitForHealthAsync(int port)
    {
        using var client = new HttpClient();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync($"http://localhost:{port}/health")).IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Bolt test host did not become healthy within 10 seconds.");
    }

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
