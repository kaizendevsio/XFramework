using Microsoft.Extensions.FileProviders;
using Serilog;
using XFramework.Blazor.Core.Interfaces;
using XFramework.Domain.Shared.Extensions;

namespace XFramework.Blazor.Core.Extensions;

public static class InstallerExtensions
{
    
    public static void InstallBlazorBaseServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton(o => new DeviceAgentProvider(Environment.MachineName));
        services.AddScoped<HandlerServices>();
        
        services.InstallServicesInAssembly<XFramework.Blazor.Base>(configuration, hostEnvironment);
        InstallRuntimeServices(services, configuration, hostEnvironment);
    }
    
    public static void InstallRuntimeServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton(o => new DeviceAgentProvider(Environment.MachineName));
        services.AddScoped<HandlerServices>();
    }
    
    public static void AddSerilog(this IServiceCollection services, IConfiguration configuration)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .Enrich.FromLogContext() // This will ensure SourceContext is populated
            .WriteTo.Async(a => a.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} => {Message:lj}{NewLine}{Exception}"));
        
        Log.Logger = loggerConfiguration.CreateLogger();
        services.AddSingleton(Log.Logger);
    }
    
    
}