using XFramework.Domain.Shared.Extensions;

namespace XFramework.Blazor.Core.Extensions;

public static class InstallerExtensions
{

    public static void InstallBlazorBaseServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton(o => new DeviceAgentProvider(Environment.MachineName));
        services.AddScoped<HandlerServices>();

        services.InstallServicesInAssembly<XFramework.Blazor.Base>(configuration, hostEnvironment);
    }
}