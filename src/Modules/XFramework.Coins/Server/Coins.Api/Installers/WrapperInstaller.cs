using IdentityServer.Integration.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace Coins.Api.Installers;

public sealed class WrapperInstaller : IInstaller
{
    public void InstallServices<TApp>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkBoltClient(configuration, hostEnvironment: hostEnvironment);
        services.AddIdentityServerSessionValidation();
    }
}
