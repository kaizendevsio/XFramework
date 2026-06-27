using IdentityServer.Integration.Drivers;
using Communications.Integration.Drivers;
using Storage.Integration.Drivers;
using Wallets.Integration.Drivers;

namespace XFramework.Blazor.Core.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddIdentityServerWrapperServices();
        services.AddStorageWrapperServices();
        services.AddWalletsWrapperServices();
        services.AddCommunicationsWrapperServices();
    }
}
