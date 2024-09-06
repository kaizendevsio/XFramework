using StreamFlow.Core.Services;
using XFramework.Domain.Shared.Interfaces;
using ICachingService = StreamFlow.Core.Interfaces.ICachingService;

namespace StreamFlow.Stream.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton<ICachingService, CachingService>();
    }
}