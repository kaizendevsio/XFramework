using Inventario.Integration.Drivers;
using Wallets.Integration.Drivers;
using IdentityServer.Integration.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace POS.Api.Installers;

public sealed class WrapperInstaller : IInstaller
{
    public void InstallServices<TAssembly>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkBoltClient(configuration, hostEnvironment: hostEnvironment);
        services.AddIdentityServerSessionValidation();
        services.AddInventarioWrapperServices();
        services.AddWalletsWrapperServices();
    }
}
