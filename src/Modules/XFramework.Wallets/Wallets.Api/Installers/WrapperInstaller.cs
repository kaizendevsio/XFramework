using XFramework.Domain.Shared.Interfaces;
using IdentityServer.Integration.Extensions;
using XFramework.Integration.Extensions;

namespace Wallets.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkBoltClient(configuration, hostEnvironment: hostEnvironment);
        services.AddIdentityServerSessionValidation();
    }
}
