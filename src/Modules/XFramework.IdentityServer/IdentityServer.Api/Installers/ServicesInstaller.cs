using Messaging.Integration.Drivers;
using XFramework.Core.Extensions;

namespace IdentityServer.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddMessagingWrapperServices();
        services.AddTenantResolver();

        // Register AuthService for VSA pattern
        services.AddScoped<IAuthService, AuthService>();
    }
}