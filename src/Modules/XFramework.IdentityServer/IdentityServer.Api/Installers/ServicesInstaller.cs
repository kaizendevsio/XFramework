using Communications.Integration.Drivers;
using Storage.Integration.Drivers;
using XFramework.Core.Extensions;

namespace IdentityServer.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddCommunicationsWrapperServices();
        services.AddStorageWrapperServices();
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();

        // Register AuthService for VSA pattern
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IIdentityAuthorizationService, IdentityAuthorizationService>();
    }
}
