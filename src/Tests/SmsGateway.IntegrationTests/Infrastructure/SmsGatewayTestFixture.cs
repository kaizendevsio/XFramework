using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using XFramework.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmsGateway.Api.Services;
using Testcontainers.PostgreSql;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using Contracts = XFramework.Domain.Shared.Contracts;

namespace SmsGateway.IntegrationTests;

[SetUpFixture]
public class SmsGatewayTestFixture
{
    private static PostgreSqlContainer _postgres = null!;
    private static WebApplication _app = null!;
    private static Task? _appTask;

    public static string ConnectionString { get; private set; } = null!;
    public static string AppUrl => "http://localhost:18561";
    public static readonly Guid TestTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");
    public static readonly Guid TestAgentClusterId = Guid.Parse("a1a2a3a4-b5b6-c7c8-d9d0-e1e2e3e4e5e6");

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("XFramework_SmsGateway_Test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        // Migrate DB (needed for app startup even though SMS ops are in-memory)
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        if (!await db.Set<Contracts.Tenant>().AnyAsync(t => t.Id == TestTenantId))
        {
            db.Set<Contracts.Tenant>().Add(new Contracts.Tenant
            {
                Id = TestTenantId, TenantId = TestTenantId,
                Name = "Test Tenant", Description = "SmsGateway test"
            });
            await db.SaveChangesAsync();
        }

        _app = StartApp();
        await WaitForHealth($"{AppUrl}/health/live");
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        try { if (_app != null) await _app.StopAsync(); } catch { }
        if (_postgres != null) await _postgres.DisposeAsync();
    }

    private static WebApplication StartApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(AppUrl);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = ConnectionString,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Warning"
        });

        // Register services directly — SmsGateway is simple (in-memory cache, no StreamFlow)
        builder.Services.AddSingleton<ICachingService, CachingService>();
        builder.Services.AddScoped<ISmsService, SmsService>();
        builder.Services.AddValidatorsFromAssemblyContaining<SmsService>();

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        SmsGateway.Api.Generated.GeneratedEndpointRoutes.MapGeneratedEndpoints(app);
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _appTask = Task.Run(() => app.RunAsync());
        return app;
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
}
