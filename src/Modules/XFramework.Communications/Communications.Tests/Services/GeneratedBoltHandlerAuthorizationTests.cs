using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Bolt.Client;
using Bolt.Protocol;
using Bolt.Server;
using Communications.Api.Features.Settings;
using Communications.Api.Features.Threads.Update;
using Communications.Api.Services;
using Communications.Domain.Shared.Contracts.Requests.Settings;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using FluentValidation;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Security;

namespace Communications.Tests.Services;

public sealed class GeneratedBoltHandlerAuthorizationTests
{
    [TestCase(401)]
    [TestCase(403)]
    [TestCase(503)]
    public async Task GeneratedHandler_AuthorizationFailure_ReturnsBeforeEndpointServiceResolution(int statusCode)
    {
        var authorizer = new StubAuthorizer(
            TrustedServiceInvocationResult.Failure("denied", statusCode));
        using var provider = CreateProvider(authorizer);
        await using var client = CreateClient();
        var handler = RegisterGeneratedHandler(client, provider);
        var context = new BoltInboundRequestContext(Guid.NewGuid(), 12345);
        var payload = MemoryPackSerializer.Serialize(new GetCommunicationsSettingsRequest
        {
            Metadata = new RequestMetadata { ServiceAccessToken = "token" }
        });

        var result = await handler(payload, context, CancellationToken.None);

        Assert.That(result.Item1, Is.EqualTo((HttpStatusCode)statusCode));
        Assert.That(authorizer.LastContext, Is.EqualTo(context));
        Assert.That(authorizer.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GeneratedHandler_ValidAuthorization_ContinuesToEndpointService()
    {
        var invocation = new TrustedServiceInvocation(
            XFrameworkServiceNames.Portal,
            XFrameworkServiceNames.Communications,
            null,
            null,
            new RequestMetadata { ServiceAccessToken = "valid" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var authorizer = new StubAuthorizer(TrustedServiceInvocationResult.Success(invocation));
        var settingsService = new StubSettingsService();
        using var provider = CreateProvider(authorizer, settingsService);
        await using var client = CreateClient();
        var handler = RegisterGeneratedHandler(client, provider);

        var result = await handler(
            MemoryPackSerializer.Serialize(new GetCommunicationsSettingsRequest
            {
                Metadata = new RequestMetadata { ServiceAccessToken = "valid" }
            }),
            new BoltInboundRequestContext(Guid.NewGuid(), 12345),
            CancellationToken.None);

        Assert.That(result.Item1, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(settingsService.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GeneratedHandler_ValidAuthorizationThenValidationFailure_PreservesBadRequestEnvelope()
    {
        var invocation = new TrustedServiceInvocation(
            XFrameworkServiceNames.Portal,
            XFrameworkServiceNames.Communications,
            null,
            null,
            new RequestMetadata { ServiceAccessToken = "valid" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var authorizer = new StubAuthorizer(TrustedServiceInvocationResult.Success(invocation));
        var services = new ServiceCollection();
        services.AddSingleton<IBoltServiceInvocationAuthorizer>(authorizer);
        services.AddSingleton<IValidator<UpdateThreadRequest>, UpdateThreadValidator>();
        using var provider = services.BuildServiceProvider();
        await using var client = CreateClient();
        var handler = RegisterGeneratedHandler(
            client,
            provider,
            "Communications.Api.Features.Threads.Update.Generated.UpdateThreadEndpoint_Handle_BoltHandler",
            nameof(UpdateThreadRequest));

        var result = await handler(
            MemoryPackSerializer.Serialize(new UpdateThreadRequest
            {
                Metadata = new RequestMetadata { ServiceAccessToken = "valid" }
            }),
            new BoltInboundRequestContext(Guid.NewGuid(), 12345),
            CancellationToken.None);
        var response = MemoryPackSerializer.Deserialize<CmdResponse>(result.Item2.Span);

        Assert.That(result.Item1, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Message, Is.EqualTo("Validation failed"));
        Assert.That(response.ValidationErrors, Does.ContainKey(nameof(UpdateThreadRequest.ThreadId)));
    }

    [TestCase(401)]
    [TestCase(403)]
    [TestCase(200)]
    public async Task GeneratedHandler_LargeRpcPath_EnforcesSameAuthorization(int statusCode)
    {
        var invocation = new TrustedServiceInvocation(
            XFrameworkServiceNames.Portal,
            XFrameworkServiceNames.Communications,
            null,
            null,
            new RequestMetadata { ServiceAccessToken = "valid" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var authorization = statusCode == 200
            ? TrustedServiceInvocationResult.Success(invocation)
            : TrustedServiceInvocationResult.Failure("denied", statusCode);
        var authorizer = new StubAuthorizer(authorization);
        var settingsService = new StubSettingsService();
        using var provider = CreateProvider(authorizer, settingsService);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBoltServer();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        await using var app = builder.Build();
        app.UseWebSockets();
        app.MapBolt("/bolt");
        await app.StartAsync();
        var address = app.Urls.Single();
        var boltUri = new Uri(address.Replace("http://", "ws://", StringComparison.Ordinal) + "/bolt");
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        await using var caller = CreateNetworkClient(boltUri, "generated-caller", loggerFactory);
        await using var recipient = CreateNetworkClient(boltUri, "generated-recipient", loggerFactory);
        RegisterGeneratedHandler(recipient, provider);

        await recipient.ConnectAsync();
        await caller.ConnectAsync();
        var result = await caller.InvokeAsync(
            "generated-recipient",
            nameof(GetCommunicationsSettingsRequest),
            MemoryPackSerializer.Serialize(new GetCommunicationsSettingsRequest
            {
                Metadata = new RequestMetadata { ServiceAccessToken = "token" }
            }));

        Assert.That((int)result.StatusCode, Is.EqualTo(statusCode));
        Assert.That(authorizer.CallCount, Is.EqualTo(1));
        Assert.That(authorizer.LastContext.SenderHash, Is.EqualTo(BoltCodec.Fnv1aHash("generated-caller")));
        Assert.That(settingsService.CallCount, Is.EqualTo(statusCode == 200 ? 1 : 0));
    }

    [Test]
    public async Task GeneratedHandler_WarmAuthorization_RecordsCpuLatencyAndAllocationEvidence()
    {
        const int iterations = 20_000;
        using var rsa = RSA.Create(2048);
        var keyId = Guid.NewGuid().ToString("N");
        var token = CreateServiceToken(rsa, keyId);
        var validator = new ServiceTokenValidator(
            new StaticSigningKeyProvider(rsa.ExportSubjectPublicKeyInfoPem(), keyId),
            Options.Create(new ServiceIdentityOptions { Issuer = "XFramework.IdentityServer" }));
        var authorizer = new BoltServiceInvocationAuthorizer(
            new TrustedServiceInvocationResolver(validator),
            Options.Create(new ServiceIdentityOptions { ClientId = XFrameworkServiceNames.Communications }));
        var settingsService = new StubSettingsService();
        using var provider = CreateProvider(authorizer, settingsService);
        await using var client = CreateClient();
        var handler = RegisterGeneratedHandler(client, provider);
        var payload = MemoryPackSerializer.Serialize(new GetCommunicationsSettingsRequest
        {
            Metadata = new RequestMetadata { ServiceAccessToken = token }
        });
        var context = new BoltInboundRequestContext(
            Guid.NewGuid(),
            BoltCodec.Fnv1aHash(XFrameworkServiceNames.Portal.ToSha256()));

        for (var index = 0; index < 1_000; index++)
            await handler(payload, context, CancellationToken.None);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var timings = new long[iterations];
        var allocationBefore = GC.GetTotalAllocatedBytes(precise: true);
        var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        for (var index = 0; index < iterations; index++)
        {
            var started = Stopwatch.GetTimestamp();
            var result = await handler(payload, context, CancellationToken.None);
            timings[index] = Stopwatch.GetTimestamp() - started;
            if (result.Item1 != HttpStatusCode.OK)
                throw new InvalidOperationException($"Unexpected generated-handler status {result.Item1}.");
        }
        wall.Stop();
        var cpu = Process.GetCurrentProcess().TotalProcessorTime - cpuBefore;
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocationBefore;
        Array.Sort(timings);
        var meanMicroseconds = wall.Elapsed.TotalMicroseconds / iterations;
        var p95Microseconds = timings[(int)(iterations * 0.95) - 1] * 1_000_000d / Stopwatch.Frequency;

        TestContext.Progress.WriteLine(
            $"GeneratedAuthorizationWarm iterations={iterations} mean_us={meanMicroseconds:F3} " +
            $"p95_us={p95Microseconds:F3} throughput_ops_s={iterations / wall.Elapsed.TotalSeconds:F0} " +
            $"allocated_bytes_op={(double)allocated / iterations:F1} cpu_ms={cpu.TotalMilliseconds:F1}");
    }

    private static string CreateServiceToken(RSA rsa, string keyId)
    {
        var key = new RsaSecurityKey(rsa) { KeyId = keyId };
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            issuer: "XFramework.IdentityServer",
            audience: XFrameworkServiceNames.Communications,
            claims:
            [
                new Claim("client_id", XFrameworkServiceNames.Portal),
                new Claim(JwtRegisteredClaimNames.Sub, XFrameworkServiceNames.Portal),
                new Claim("scope", XFrameworkServiceScopes.BoltService)
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        jwt.Header["kid"] = keyId;
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static ServiceProvider CreateProvider(
        IBoltServiceInvocationAuthorizer authorizer,
        ICommunicationsSettingsService? settingsService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(authorizer);
        if (settingsService is not null)
            services.AddSingleton(settingsService);
        return services.BuildServiceProvider();
    }

    private static BoltClient CreateClient() =>
        new(
            new Uri("ws://localhost/bolt"),
            "generated-handler-test",
            "generated-handler-test",
            new BoltClientOptions(),
            NullLogger<BoltClient>.Instance);

    private static BoltClient CreateNetworkClient(Uri uri, string clientId, ILoggerFactory loggerFactory) =>
        new(
            uri,
            clientId,
            clientId,
            new BoltClientOptions
            {
                RpcTimeoutSeconds = 10,
                LargePayloadThreshold = 1,
                StreamChunkSize = 1024,
                MaxConnections = 1
            },
            loggerFactory.CreateLogger<BoltClient>());

    private static Func<ReadOnlyMemory<byte>, BoltInboundRequestContext, CancellationToken,
        Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> RegisterGeneratedHandler(
        BoltClient client,
        ServiceProvider provider)
        => RegisterGeneratedHandler(
            client,
            provider,
            "Communications.Api.Features.Settings.Generated.GetCommunicationsSettingsEndpoint_Handle_BoltHandler",
            nameof(GetCommunicationsSettingsRequest));

    private static Func<ReadOnlyMemory<byte>, BoltInboundRequestContext, CancellationToken,
        Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> RegisterGeneratedHandler(
        BoltClient client,
        ServiceProvider provider,
        string generatedTypeName,
        string commandName)
    {
        var generatedType = typeof(GetCommunicationsSettingsEndpoint).Assembly.GetType(generatedTypeName, throwOnError: true)!;
        var generatedHandler = (IBoltHandler)Activator.CreateInstance(generatedType)!;
        generatedHandler.Register(
            client,
            NullLogger.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        var handlers = (ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, BoltInboundRequestContext,
            CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>>)typeof(BoltClient)
            .GetField("_handlers", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(client)!;
        return handlers[BoltCodec.Fnv1aHash(commandName)];
    }

    private sealed class StubAuthorizer(TrustedServiceInvocationResult result)
        : IBoltServiceInvocationAuthorizer
    {
        public int CallCount { get; private set; }
        public BoltInboundRequestContext LastContext { get; private set; }

        public Task<TrustedServiceInvocationResult> AuthorizeAsync(
            RequestMetadata? metadata,
            BoltInboundRequestContext requestContext,
            IReadOnlyCollection<string>? requiredScopes = null,
            IReadOnlyCollection<string>? allowedCallers = null,
            CancellationToken ct = default)
        {
            CallCount++;
            LastContext = requestContext;
            return Task.FromResult(result);
        }
    }

    private sealed class StubSettingsService : ICommunicationsSettingsService
    {
        public int CallCount { get; private set; }

        public Task<Result<CommunicationsSettingsResponse>> GetSettingsAsync(
            GetCommunicationsSettingsRequest request,
            CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(Result<CommunicationsSettingsResponse>.Success(new CommunicationsSettingsResponse()));
        }

        public Task<Result<CommunicationsSettingsResponse>> UpdateSettingsAsync(
            UpdateCommunicationsSettingsRequest request,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class StaticSigningKeyProvider(string publicKeyPem, string keyId)
        : IIdentitySigningKeyProvider
    {
        public Task<IReadOnlyList<ServiceSigningKeyResponse>> GetSigningKeysAsync(
            string? requestedKeyId = null,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ServiceSigningKeyResponse>>(
            [
                new ServiceSigningKeyResponse
                {
                    KeyId = keyId,
                    Algorithm = "RS256",
                    PublicKeyPem = publicKeyPem,
                    CreatedAtUtc = DateTime.UtcNow,
                    ActivatedAtUtc = DateTime.UtcNow,
                    IsActive = true
                }
            ]);
    }
}
