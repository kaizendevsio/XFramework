using Communications.Integration.Drivers;
using IdentityServer.Integration.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Extensions;

namespace Inventario.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkBoltClient(configuration, hostEnvironment: hostEnvironment);
        services.AddIdentityServerSessionValidation();
        services.AddScoped<ICommunicationsServiceWrapper, CommunicationsServiceWrapper>();
    }
}
