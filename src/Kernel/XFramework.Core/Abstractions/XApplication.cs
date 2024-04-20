using Microsoft.EntityFrameworkCore;
using XFramework.Integration.Abstractions;

namespace XFramework.Core.Abstractions;

public static class XApplication
{
    public static IApplicationBuilder Build<T>()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseSerilog();
        
        var configuration = builder.Configuration;
        var services = builder.Services;
        
        services.InstallServicesInAssembly<T>(configuration);
        services.InstallSwagger(configuration);
        services.InstallOData(configuration);
        services.InstallJwt(configuration);
        services.InstallStandardServices<T>(configuration);
        services.InstallRuntimeServices(configuration);
        
        var app = builder.Build();
        app.UseCustomMiddleware();
        app.UseConfiguredSwagger();
        app.UseXFrameworkEndpoints();
        app.UseEndpointsInAssembly(app.Environment);
        app.WarmUpServices(services, ServiceLifetime.Singleton);
       
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
        using var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope();
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
        (app as WebApplication).Run();
    }
}