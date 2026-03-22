using System.Net.Http.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using FluentValidation;
using IdentityServer.Api.Generated;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Stream.Extensions;
using Testcontainers.PostgreSql;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Interfaces;
using IdentityServer.Domain.Shared.Contracts;
using XFramework.Extensions;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Drivers;
using Contracts = IdentityServer.Domain.Shared.Contracts;

namespace IdentityServer.Benchmarks;

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class TransportBenchmarks
{
    private PostgreSqlContainer _postgres = null!;
    private WebApplication _streamFlowApp = null!;
    private WebApplication _identityServerApp = null!;
    private WebApplication _testClientApp = null!;
    private Task? _streamFlowTask;
    private Task? _identityServerTask;
    private Task? _testClientTask;
    private HttpClient _httpClient = null!;
    private IIdentityServerServiceWrapper _serviceWrapper = null!;

    private const string StreamFlowUrl = "http://localhost:19000";
    private const string IdentityServerUrl = "http://localhost:19261";
    private const string TestClientUrl = "http://localhost:19262";
    private static readonly Guid TestTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");

    private HealthCheckRequest _request = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        // 1. Start Postgres
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("XFramework_Bench")
            .WithUsername("bench_user")
            .WithPassword("bench_password")
            .Build();
        await _postgres.StartAsync();

        // 2. Migrate
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        if (!await db.Set<Contracts.Tenant>().AnyAsync(t => t.Id == TestTenantId))
        {
            db.Set<Contracts.Tenant>().Add(new Contracts.Tenant
            {
                Id = TestTenantId, TenantId = TestTenantId,
                Name = "Bench Tenant", Description = "Benchmark tenant"
            });
            await db.SaveChangesAsync();
        }

        // 3. Start StreamFlow hub
        _streamFlowApp = StartStreamFlow();
        await WaitForHealth($"{StreamFlowUrl}/health/live");

        // 4. Start IdentityServer
        _identityServerApp = StartIdentityServer();
        await WaitForHealth($"{IdentityServerUrl}/health/live");

        // 5. Seed tenant cache
        var cache = _identityServerApp.Services.GetRequiredService<IMemoryCache>();
        cache.Set($"GetTenant-{TestTenantId}", new Contracts.Tenant
        {
            Id = TestTenantId, TenantId = TestTenantId,
            Name = "Bench Tenant", Description = "Benchmark tenant"
        });

        // 6. Start test client (StreamFlow consumer)
        _testClientApp = StartTestClient();
        await WaitForHealth($"{TestClientUrl}/health/live");

        // 7. Wait for StreamFlow clients to connect
        await WaitForStreamFlowClients();

        // 8. Register StreamFlow handlers
        RegisterStreamFlowHandlers();

        // 9. Create reusable objects
        _httpClient = new HttpClient { BaseAddress = new Uri(IdentityServerUrl) };
        _serviceWrapper = _testClientApp.Services.GetRequiredService<IIdentityServerServiceWrapper>();
        _request = new HealthCheckRequest
        {
            Metadata = new RequestMetadata
            {
                TenantId = TestTenantId,
                RequestId = Guid.NewGuid(),
                IpAddress = "127.0.0.1",
                Name = "Benchmark",
                DeviceName = "BenchDevice",
                DeviceAgent = "BenchAgent"
            }
        };

        // Warmup both paths
        for (var i = 0; i < 5; i++)
        {
            await _httpClient.PostAsJsonAsync("/api/health/check", _request);
            await _serviceWrapper.HealthCheck(new HealthCheckRequest
            {
                Metadata = new RequestMetadata
                {
                    TenantId = TestTenantId, RequestId = Guid.NewGuid(),
                    IpAddress = "127.0.0.1", Name = "Benchmark",
                    DeviceName = "BenchDevice", DeviceAgent = "BenchAgent"
                }
            });
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _httpClient?.Dispose();
        try { await _testClientApp.StopAsync(); } catch { }
        try { await _identityServerApp.StopAsync(); } catch { }
        try { await _streamFlowApp.StopAsync(); } catch { }
        await _postgres.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task<HttpResponseMessage> Http_HealthCheck()
    {
        return await _httpClient.PostAsJsonAsync("/api/health/check", _request);
    }

    [Benchmark]
    public async Task<QueryResponse<IdentityServer.Domain.Shared.Contracts.Responses.HealthCheckResponse>?> StreamFlow_HealthCheck()
    {
        // Each call needs a unique RequestId to avoid idempotency guard
        var req = new HealthCheckRequest
        {
            Metadata = new RequestMetadata
            {
                TenantId = TestTenantId,
                RequestId = Guid.NewGuid(),
                IpAddress = "127.0.0.1",
                Name = "Benchmark",
                DeviceName = "BenchDevice",
                DeviceAgent = "BenchAgent"
            }
        };
        return await _serviceWrapper.HealthCheck(req);
    }

    #region Infrastructure

    private WebApplication StartStreamFlow()
    {
        var builder = XApplication.Configure<StreamFlow.Stream.Installers.StreamInstaller>();
        builder.WebHost.UseUrls(StreamFlowUrl);
        OverrideConfig(builder, "StreamFlow.Bench", "00000000-0000-0000-0000-000000000099");

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.UseAppServices();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _streamFlowTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private WebApplication StartIdentityServer()
    {
        var builder = XApplication.Configure<AuthService>();
        builder.WebHost.UseUrls(IdentityServerUrl);
        OverrideConfig(builder, "IdentityServer.Bench", "3902761a-822d-4c6b-8e2d-323fd501bcd6");
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

    private WebApplication StartTestClient()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(TestClientUrl);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["StreamFlowConfiguration:ClientName"] = "BenchClient",
            ["StreamFlowConfiguration:ServerUrls:0"] = $"{StreamFlowUrl}/stream-flow/queue",
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Error",
        });

        builder.Services.InstallStandardServices<TransportBenchmarks>(builder.Configuration);
        builder.Services.AddSingleton(new DeviceAgentProvider("Benchmark"));
        builder.Services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        builder.Services.AddIdentityServerWrapperServices();

        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _testClientTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private async Task WaitForStreamFlowClients()
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

    private void RegisterStreamFlowHandlers()
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

    private void OverrideConfig(WebApplicationBuilder builder, string clientName, string clientGuid)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = _postgres.GetConnectionString(),
            ["StreamFlowConfiguration:ClientGuid"] = clientGuid,
            ["StreamFlowConfiguration:ClientName"] = clientName,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["JwtOptions:ValidAudience"] = IdentityServerUrl,
            ["JwtOptions:ValidIssuer"] = IdentityServerUrl,
            ["JwtOptions:Secret"] = "Mm1VFHaqZ7MoVJyZd1zrAKxTpsXbYG6RqSMKYG2cV7RBBUdmsm97HOfKyA7MZ1LUl77ZklJPJfnegohyHqJIoQ983fTKmJcY",
            ["JwtOptions:AccessTokenLifespan"] = "00:30:00",
            ["JwtOptions:RefreshTokenLifespan"] = "00:30:00",
            ["Serilog:MinimumLevel:Default"] = "Error"
        });
    }

    private static async Task WaitForHealth(string url, int timeoutSeconds = 30)
    {
        using var client = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }

    #endregion
}

file class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        AddJob(Job.ShortRun
            .WithWarmupCount(3)
            .WithIterationCount(10));
        AddColumn(StatisticColumn.P95);
        AddColumn(new OpsPerSecColumn());
    }
}

file class OpsPerSecColumn : IColumn
{
    public string Id => "OpsPerSec";
    public string ColumnName => "Op/s";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Operations per second (1 / Mean)";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        => GetValue(summary, benchmarkCase, SummaryStyle.Default);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var report = summary[benchmarkCase];
        if (report?.ResultStatistics == null) return "N/A";
        var meanNs = report.ResultStatistics.Mean;
        var opsPerSec = 1_000_000_000.0 / meanNs;
        return opsPerSec.ToString("N0");
    }

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    public bool IsAvailable(Summary summary) => true;
}
