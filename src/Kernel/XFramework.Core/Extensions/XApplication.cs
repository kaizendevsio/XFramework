using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.Extensions;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Extensions;

namespace XFramework.Core.Extensions;

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
        app.UseCustomMiddleware();
        app.UseStandardMiddleware();
        app.UseConfiguredSwagger();
        app.UseXFrameworkEndpoints();
        app.UseEndpointsInAssembly(app.Environment);
        app.WarmUpServices(builder.Services, ServiceLifetime.Singleton);
       
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
        using var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>()!.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<TDbContext>();

        var canMigrate = dbContext.Database.GetPendingMigrations().Any();
        if (!canMigrate)
        {
            dbContext.Database.EnsureCreated();
        }
        else
        {
            dbContext.Database.Migrate();
        }

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
            .AddInteractiveServerRenderMode();
      
        app.UseStaticFiles();
        return app;
    }
}