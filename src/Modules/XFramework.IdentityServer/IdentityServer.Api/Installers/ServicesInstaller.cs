using IdentityServer.Core;
using IdentityServer.Core.Services;
using Messaging.Integration.Drivers;
using Tenant.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace IdentityServer.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddIdentityServerWrapperServices();
        services.AddTenantWrapperServices();
        services.AddMessagingWrapperServices();
        services.AddDecoratorHandlers(typeof(IdentityServerCore).Assembly);
        services.AddTenantService();
        
        // Register AuthService for VSA pattern
        services.AddScoped<IAuthService, AuthService>();
    }
}