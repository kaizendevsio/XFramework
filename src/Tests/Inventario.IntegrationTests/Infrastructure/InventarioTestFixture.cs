using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using XFramework.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Domain.Contexts;
using XFramework.TestInfrastructure;
using Contracts = XFramework.Domain.Shared.Contracts;

namespace Inventario.IntegrationTests;

[SetUpFixture]
public class InventarioTestFixture
{
    private static PostgreSqlContainer _postgres = null!;
    private static WebApplication _app = null!;
    private static Task? _appTask;

    public static string ConnectionString { get; private set; } = null!;
    public static string AppUrl => TestConstants.Ports.InventarioServer;

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

        _app = StartApp();
        await StreamFlowTestHelper.WaitForHealth($"{AppUrl}/health/live", _appTask);

        // Seed data using the app's DbContext (which has Inventario entity mappings)
        await SeedData();
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
            ["Tenant:DefaultId"] = TestConstants.TenantId.ToString(),
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

        // Map feature endpoints (source-generated)
        Inventario.Api.Generated.GeneratedEndpointRoutes.MapGeneratedEndpoints(app);
        app.MapGet("/health/live", () => Results.Ok("healthy"));

        _appTask = Task.Run(() => app.RunAsync());
        return app;
    }

    private static async Task SeedData()
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seed tenant
        if (!await db.Set<Contracts.Tenant>().AnyAsync(t => t.Id == TestConstants.TenantId))
        {
            db.Set<Contracts.Tenant>().Add(new Contracts.Tenant
            {
                Id = TestConstants.TenantId,
                TenantId = TestConstants.TenantId,
                Name = "Test Tenant",
                Description = "Inventario test tenant"
            });
        }

        // Seed product category (uses app's DbContext which has Inventario entity mappings)
        if (!await db.Set<XFramework.Inventario.Domain.Shared.Contracts.ProductCategory>()
                .AnyAsync(c => c.Id == TestConstants.ProductCategoryId))
        {
            db.Set<XFramework.Inventario.Domain.Shared.Contracts.ProductCategory>().Add(
                new XFramework.Inventario.Domain.Shared.Contracts.ProductCategory
                {
                    Id = TestConstants.ProductCategoryId,
                    Name = "Test Category",
                    TenantId = TestConstants.TenantId
                });
        }

        await db.SaveChangesAsync();
    }
}
