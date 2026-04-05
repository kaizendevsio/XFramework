using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Extensions;
using XFramework.Integration.Abstractions;

// ReSharper disable once CheckNamespace
namespace XFramework.Extensions;

public static class XApplication
{
    public static IApplicationBuilder Build<T>()
    {
        var builder = Configure<T>();
        return Build(builder);
    }

    public static WebApplicationBuilder Configure<T>()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseSerilog();
        
        var configuration = builder.Configuration;
        var services = builder.Services;
        
        services.InstallServicesInAssembly<T>(configuration, builder.Environment);
        services.InstallSwagger(configuration);
        services.InstallOData(configuration);
        services.InstallJwt(configuration);
        services.InstallStandardServices<T>(configuration);
        services.InstallRuntimeServices(configuration);
        
        return builder;
    }
    
    public static IApplicationBuilder Build(this WebApplicationBuilder builder)
    {
        var app = builder.Build();
        
        // Custom middleware (headers, etc.)
        app.UseCustomMiddleware();
        
        // Standard middleware (exception handling, HTTPS, CORS, routing, auth)
        app.UseStandardMiddleware();
        
        // Response compression (must come BEFORE output caching)
        app.UseConfiguredResponseCompression();
        
        // Output caching (caches compressed responses)
        app.UseConfiguredOutputCaching();
        
        // Application endpoints
        app.UseXFrameworkEndpoints();
        app.UseEndpointsInAssembly(app.Environment);
        
        // Warm up singleton services
        app.WarmUpServices(builder.Services, ServiceLifetime.Singleton);
        
        // HTTPS redirection (moved to UseStandardMiddleware for proper ordering)
        // app.UseHttpsRedirection(); // Already in UseStandardMiddleware
        
        return app;
    }
    
    public static IApplicationBuilder UseCustomRequestsInAssembly<T>(this IApplicationBuilder app)
    {
        var signalRService = app.ApplicationServices.GetRequiredService<ISignalRService>();
        
        signalRService.AddHandlersFromAssembly<T>();
        return app;
    }

    public static IApplicationBuilder EnsureDatabase<TDbContext>(this IApplicationBuilder app)
        where TDbContext : DbContext
    {
        // When running in Docker, migrations are handled by the MigrationRunner init container.
        // Set SKIP_DB_MIGRATION=true in docker-compose to skip self-migration.
        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
        if (configuration.GetValue<bool>("SKIP_DB_MIGRATION"))
        {
            return app;
        }

        using var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>()!.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<TDbContext>();

        if (dbContext.Database.GetPendingMigrations().Any())
        {
            dbContext.Database.Migrate();
        }

        return app;
    }
    
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
        return app;
    }

    public static void Run(this IApplicationBuilder app)
    {
        (app as WebApplication)!.Run();
    }
    
    public static async Task RunAsync(this IApplicationBuilder app)
    {
        await (app as WebApplication)!.RunAsync();
    }
    
    public static IApplicationBuilder UseBlazor<TApp>(this IApplicationBuilder app)
    {
        (app as WebApplication)!
            .MapRazorComponents<TApp>()
            .WithStaticAssets()
            .AddInteractiveServerRenderMode();

        (app as WebApplication)!.MapStaticAssets();
      
        app.UseAntiforgery();
        app.UseStaticFiles();
        app.UseWebOptimizer();
        
        return app;
    }
}