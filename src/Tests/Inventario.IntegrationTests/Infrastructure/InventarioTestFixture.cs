using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using XFramework.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using Contracts = XFramework.Domain.Shared.Contracts;

namespace Inventario.IntegrationTests;

[SetUpFixture]
public class InventarioTestFixture
{
    private static PostgreSqlContainer _postgres = null!;
    private static WebApplication _app = null!;
    private static Task? _appTask;

    public static string ConnectionString { get; private set; } = null!;
    public static string AppUrl => "http://localhost:18461";

    public static readonly Guid TestTenantId = Guid.Parse("7602c2d3-01df-4bdb-9a67-02c144e4a2ac");
    public static readonly Guid TestCategoryId = Guid.Parse("f1f2f3f4-e5f6-7890-abcd-ef1234567890");

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        _postgres = new PostgreSqlBuilder()
            .WithDatabase("XFramework_Inventario_Test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        await MigrateAndSeed();

        _app = StartApp();
        await WaitForHealth($"{AppUrl}/health/live", _appTask);
    }

    [OneTimeTearDown]
    public async Task GlobalTeardown()
    {
        try { if (_app != null) await _app.StopAsync(); } catch { }
        if (_postgres != null) await _postgres.DisposeAsync();
    }

    private static WebApplication StartApp()
    {
        var builder = XApplication.Configure<XFramework.Inventario.Api.Services.ProductService>();
        builder.WebHost.UseUrls(AppUrl);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultDatabaseConnection"] = ConnectionString,
            ["Tenant:DefaultId"] = TestTenantId.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Warning"
        });

        builder.Services.AddMemoryCaching();
        builder.Services.AddSingleton<IDistributedCache>(sp => null!);
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp => null!);
        builder.Services.AddValidatorsFromAssemblyContaining<XFramework.Inventario.Api.Services.ProductService>();
        builder.Services.AddGeneratedServices();

        var app = (WebApplication)builder.Build();
        app.UseCorrelationId();
        app.EnsureDatabase<AppDbContext>();

        // Map feature endpoints (source-generated with [AsParameters] for GET binding)
        Inventario.Api.Generated.GeneratedEndpointRoutes.MapGeneratedEndpoints(app);
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _appTask = Task.Run(() => app.RunAsync());
        return app;
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
                Id = TestTenantId, TenantId = TestTenantId,
                Name = "Test Tenant", Description = "Inventario test tenant"
            });
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
                throw new InvalidOperationException(
                    $"App crashed: {appTask.Exception?.GetBaseException().Message}",
                    appTask.Exception?.GetBaseException());
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(500);
        }
        if (appTask is { IsFaulted: true })
            throw new InvalidOperationException(
                $"App crashed: {appTask.Exception?.GetBaseException().Message}",
                appTask.Exception?.GetBaseException());
        throw new TimeoutException($"Service at {url} did not become healthy within {timeoutSeconds}s");
    }
}
