using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Serilog;
using XFramework.Blazor.Interfaces;

namespace XFramework.Blazor.Extensions;

public static class InstallerExtensions
{
    public static void InstallServicesInAssembly<TApp>(this IServiceCollection services, IConfiguration configuration, IWebAssemblyHostEnvironment webAssemblyHostEnvironment)
    {
        var installers = typeof(TApp).Assembly.ExportedTypes
            .Where(x => typeof(IInstaller).IsAssignableFrom(x) && x is {IsInterface: false, IsAbstract: false})
            .Select(Activator.CreateInstance)
            .Cast<IInstaller>()
            .ToList();

        installers.ForEach(installer => installer.InstallServices<TApp>(services, configuration, webAssemblyHostEnvironment));
    }
    
    public static void InstallServicesInAssembly<TApp>(this IServiceCollection services, IConfiguration configuration, MockedWebAssemblyHostEnvironment mockedWebAssemblyHostEnvironment)
    {
        var installers = typeof(TApp).Assembly.ExportedTypes
            .Where(x => typeof(IInstaller).IsAssignableFrom(x) && x is {IsInterface: false, IsAbstract: false})
            .Select(Activator.CreateInstance)
            .Cast<IInstaller>()
            .ToList();

        installers.ForEach(installer => installer.InstallServices<TApp>(services, configuration, mockedWebAssemblyHostEnvironment));
    }
    
    public static void InstallRuntimeServices(this IServiceCollection services, IConfiguration configuration)
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

public class MockedWebAssemblyHostEnvironment : IWebAssemblyHostEnvironment
{
    public string Environment { get; set; }
    public string BaseAddress { get; set; }
}

public class MockedHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}