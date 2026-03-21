using IdentityServer.Api.Services;
using Messaging.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace IdentityServer.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddMessagingWrapperServices();
        services.AddTenantService();

        // Register AuthService for VSA pattern
        services.AddScoped<IAuthService, AuthService>();
    }
}