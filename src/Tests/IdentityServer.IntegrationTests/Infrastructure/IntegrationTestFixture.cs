using FluentValidation;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Services;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Stream.Extensions;
using Testcontainers.PostgreSql;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Extensions;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using Contracts = XFramework.Domain.Shared.Contracts;

namespace IdentityServer.IntegrationTests;

[SetUpFixture]
public class IntegrationTestFixture
{
    private static PostgreSqlContainer _postgres = null!;
    private static WebApplication _streamFlowApp = null!;
    private static WebApplication _identityServerApp = null!;
    private static WebApplication _testClientApp = null!;
    private static Task? _streamFlowTask;
    private static Task? _identityServerTask;
    private static Task? _testClientTask;

    public static string ConnectionString { get; private set; } = null!;
    public static string StreamFlowUrl => "http://localhost:17000";
    public static string IdentityServerUrl => "http://localhost:18261";
    public static string TestClientUrl => "http://localhost:18262";

    public static IServiceProvider Services => _identityServerApp.Services;

    /// <summary>
    /// Service wrapper that calls IdentityServer through the actual StreamFlow transport.
    /// </summary>
    public static IIdentityServerServiceWrapper ServiceWrapper =>
        _testClientApp.Services.GetRequiredService<IIdentityServerServiceWrapper>();

    public static readonly Guid TestTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        // 1. Start Postgres container
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("XFramework_Test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        // 2. Run migrations and seed data
        await MigrateAndSeed();

        // 3. Start StreamFlow hub
        _streamFlowApp = StartStreamFlow();
        await WaitForHealth($"{StreamFlowUrl}/health/live", _streamFlowTask);

        // 4. Start IdentityServer (connects to StreamFlow as "IdentityServer" client)
        _identityServerApp = StartIdentityServer();
        await WaitForHealth($"{IdentityServerUrl}/health/live", _identityServerTask);

        // 5. Seed the in-memory tenant cache
        var cache = _identityServerApp.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"GetTenant-{TestTenantId}", new Contracts.Tenant
        {
            Id = TestTenantId,
            TenantId = TestTenantId,
            Name = "Test Tenant",
            Description = "Integration test tenant"
        });

        // 6. Start test client app (connects to StreamFlow, has IIdentityServerServiceWrapper)
        _testClientApp = StartTestClient();
        await WaitForHealth($"{TestClientUrl}/health/live", _testClientTask);

        // 7. Wait for both StreamFlow clients to connect and register
        await WaitForStreamFlowClients();

        // 8. Register IdentityServer's generated StreamFlow handlers on its SignalR connection.
        //    ScanAndRegisterHandlers() only scans the entry assembly (testhost in tests),
        //    so we manually scan the IdentityServer assembly for ISignalREventHandler implementations.
        RegisterStreamFlowHandlers();
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        try { if (_testClientApp != null) await _testClientApp.StopAsync(); } catch { }
        try { if (_identityServerApp != null) await _identityServerApp.StopAsync(); } catch { }
        try { if (_streamFlowApp != null) await _streamFlowApp.StopAsync(); } catch { }
        if (_postgres != null) await _postgres.DisposeAsync();
    }

    private static WebApplication StartStreamFlow()
    {
        var builder = XApplication.Configure<StreamFlow.Stream.Installers.StreamInstaller>();
        builder.WebHost.UseUrls(StreamFlowUrl);
        OverrideConfiguration(builder, "StreamFlow.Test", "00000000-0000-0000-0000-000000000001");

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.UseAppServices();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _streamFlowTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static WebApplication StartIdentityServer()
    {
        var builder = XApplication.Configure<AuthService>();
        builder.WebHost.UseUrls(IdentityServerUrl);
        // ClientName = "IdentityServer.Test" → SignalRService registers as SHA256("IdentityServer")
        // This matches the generated wrapper's TargetClient
        OverrideConfiguration(builder, "IdentityServer.Test", "3902761a-822d-4c6b-8e2d-323fd501bcd6");
        builder.Configuration["StreamFlowConfiguration:ServerUrls:0"] = $"{StreamFlowUrl}/stream-flow/queue";

        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddValidatorsFromAssemblyContaining<AuthService>();

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.MapGeneratedEndpoints();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _identityServerTask = Task.Run(() => app.RunAsync());
        return app;
    }

    /// <summary>
    /// Minimal app that acts as a StreamFlow client with IIdentityServerServiceWrapper.
    /// This is how any real service (Blazor, Wallets, etc.) would call IdentityServer via StreamFlow.
    /// </summary>
    private static WebApplication StartTestClient()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(TestClientUrl);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["StreamFlowConfiguration:ClientName"] = "TestClient",
            ["StreamFlowConfiguration:ServerUrls:0"] = $"{StreamFlowUrl}/stream-flow/queue",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Warning",
        });

        // Core services needed by SignalRService and service wrappers
        builder.Services.InstallStandardServices<IntegrationTestFixture>(builder.Configuration);
        builder.Services.AddSingleton(new XFramework.Domain.Shared.BusinessObjects.DeviceAgentProvider("IntegrationTest"));
        builder.Services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();

        // Register the IdentityServer service wrapper (generated — uses StreamFlow transport)
        builder.Services.AddIdentityServerWrapperServices();

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _testClientTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static async Task WaitForStreamFlowClients()
    {
        var idServerSignalR = _identityServerApp.Services.GetRequiredService<ISignalRService>();
        var testClientSignalR = _testClientApp.Services.GetRequiredService<ISignalRService>();

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var idConnected = idServerSignalR.Connection?.State == HubConnectionState.Connected;
            var testConnected = testClientSignalR.Connection?.State == HubConnectionState.Connected;
            if (idConnected && testConnected)
            {
                await Task.Delay(1000);
                return;
            }
            await Task.Delay(250);
        }

        throw new TimeoutException("StreamFlow clients failed to connect within 15s");
    }

    private static void RegisterStreamFlowHandlers()
    {
        var signalRService = _identityServerApp.Services.GetRequiredService<ISignalRService>();
        var logger = _identityServerApp.Services.GetRequiredService<ILogger<BaseSignalRHandler>>();
        var scopeFactory = _identityServerApp.Services.GetRequiredService<IServiceScopeFactory>();

        var handlers = typeof(AuthService).Assembly.GetExportedTypes()
            .Where(t => typeof(ISignalREventHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(Activator.CreateInstance)
            .Cast<ISignalREventHandler>();

        foreach (var handler in handlers)
            handler.Handle(signalRService.Connection!, logger, scopeFactory);
    }

    private static void OverrideConfiguration(WebApplicationBuilder builder, string clientName, string clientGuid)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = ConnectionString,
            ["StreamFlowConfiguration:ClientGuid"] = clientGuid,
            ["StreamFlowConfiguration:ClientName"] = clientName,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["JwtOptions:ValidAudience"] = "http://localhost:18261",
            ["JwtOptions:ValidIssuer"] = "http://localhost:18261",
            ["JwtOptions:Secret"] = "Mm1VFHaqZ7MoVJyZd1zrAKxTpsXbYG6RqSMKYG2cV7RBBUdmsm97HOfKyA7MZ1LUl77ZklJPJfnegohyHqJIoQ983fTKmJcY",
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00",
            ["Serilog:MinimumLevel:Default"] = "Warning"
        });
    }

    private static async Task MigrateAndSeed()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        if (!await db.Set<Contracts.Tenant>().AnyAsync(t => t.Id == TestTenantId))
        {
            db.Set<Contracts.Tenant>().Add(new Contracts.Tenant
            {
                Id = TestTenantId,
                TenantId = TestTenantId,
                Name = "Test Tenant",
                Description = "Integration test tenant"
            });
        }

        if (!await db.Set<Contracts.IdentityVerificationType>().AnyAsync(v => v.Name == "Sms"))
        {
            db.Set<Contracts.IdentityVerificationType>().Add(new Contracts.IdentityVerificationType
            {
                Id = Guid.NewGuid(),
                Name = "Sms",
                TenantId = TestTenantId
            });
        }

        var roleGroupId = Guid.Parse("b1b2c3d4-e5f6-7890-abcd-ef1234567890");
        if (!await db.Set<Contracts.IdentityRoleTypeGroup>().AnyAsync(g => g.Id == roleGroupId))
        {
            db.Set<Contracts.IdentityRoleTypeGroup>().Add(new Contracts.IdentityRoleTypeGroup
            {
                Id = roleGroupId,
                Name = "Default",
                Description = "Default role group",
                TenantId = TestTenantId
            });
        }

        if (!await db.Set<Contracts.IdentityRoleType>().AnyAsync(r => r.Id == TestData.RoleTypeId))
        {
            db.Set<Contracts.IdentityRoleType>().Add(new Contracts.IdentityRoleType
            {
                Id = TestData.RoleTypeId,
                Name = "User",
                GroupId = roleGroupId,
                TenantId = TestTenantId
            });
        }

        var registryGroupId = Guid.Parse("c1c2c3d4-e5f6-7890-abcd-ef1234567890");
        if (!await db.Set<Contracts.RegistryConfigurationGroup>().AnyAsync(g => g.Id == registryGroupId))
        {
            db.Set<Contracts.RegistryConfigurationGroup>().Add(new Contracts.RegistryConfigurationGroup
            {
                Id = registryGroupId,
                Name = "Auth",
                Description = "Authentication configuration",
                TenantId = TestTenantId
            });
        }

        if (!await db.Set<Contracts.RegistryConfiguration>().AnyAsync(r => r.Key == "DefaultAuthorizeBy" && r.TenantId == TestTenantId))
        {
            db.Set<Contracts.RegistryConfiguration>().Add(new Contracts.RegistryConfiguration
            {
                Id = Guid.NewGuid(),
                Key = "DefaultAuthorizeBy",
                Value = "1",
                GroupId = registryGroupId,
                TenantId = TestTenantId
            });
        }

        var sessionTypes = new (string Name, Guid SystemReferenceId)[]
        {
            ("User", Guid.Parse("70b44b35-bf8e-43fc-af1a-38bdb816d51f")),
            ("Service", Guid.Parse("1e3ab070-386a-410d-823f-4f225e07a69c")),
            ("Token", Guid.Parse("d71cda39-4192-4d7b-af22-1c6c9289b913"))
        };
        foreach (var (name, sysRefId) in sessionTypes)
        {
            if (!await db.Set<Contracts.SessionType>().AnyAsync(s => s.Name == name && s.TenantId == TestTenantId))
            {
                db.Set<Contracts.SessionType>().Add(new Contracts.SessionType
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    SystemReferenceId = sysRefId,
                    TenantId = TestTenantId
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task WaitForHealth(string url, Task? appTask = null, int timeoutSeconds = 30)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            if (appTask is { IsFaulted: true })
            {
                throw new InvalidOperationException(
                    $"Application crashed during startup: {appTask.Exception?.GetBaseException().Message}",
                    appTask.Exception?.GetBaseException());
            }

            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(500);
        }

        if (appTask is { IsFaulted: true })
        {
            throw new InvalidOperationException(
                $"Application crashed during startup: {appTask.Exception?.GetBaseException().Message}",
                appTask.Exception?.GetBaseException());
        }

        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }
}

public static class TestData
{
    public static readonly Guid RoleTypeId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
}
