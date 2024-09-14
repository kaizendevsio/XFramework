using XFramework.Blazor.Core.Helpers;
using XFramework.Integration.Services;

namespace ControlPanel.Core.Installers;

public class ApiServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddHttpClient();
        services.AddScoped<IHttpClient, HttpClientHelper>();
        services.AddSingleton<CacheManager>();
        services.AddMemoryCache();
    }
}