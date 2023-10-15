namespace XFramework.Core.Abstractions;

public static class XApplication
{
    public static WebApplication Build<T>()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseSerilog();
        
        var configuration = builder.Configuration;
        var services = builder.Services;
        
        services.InstallServicesInAssembly<T>(configuration);
        services.InstallSwagger(configuration);
        services.InstallOData(configuration);
        services.InstallJwt(configuration);
        services.InstallStandardServices(configuration);
        services.InstallRuntimeServices(configuration);
        
        var app = builder.Build();
        app.UseCustomMiddleware();
        app.UseConfiguredSwagger();
        app.UseXFrameworkEndpoints();
        app.UseEndpointsInAssembly(app.Environment);
        app.WarmUpServices(services, ServiceLifetime.Singleton);
       
        return app;
    }

    public static void Run(this WebApplication app)
    {
        app.Run();
    }
}