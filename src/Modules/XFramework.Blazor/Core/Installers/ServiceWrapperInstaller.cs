using IdentityServer.Integration.Drivers;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wallets.Integration.Drivers;
using XFramework.Integration.Extensions;

namespace XFramework.Blazor.Core.Installers;

public class ServiceWrapperInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkBoltClient(configuration);

        // Service wrappers are auto-generated from [GenerateEndpoints] entities
        services.AddIdentityServerWrapperServices();
        services.AddWalletsWrapperServices();

        services.TryAddSingleton<IHelperService, HelperService>();
    }
}
