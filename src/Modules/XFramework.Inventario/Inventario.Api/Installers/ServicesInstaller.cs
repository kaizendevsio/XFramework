using FluentValidation;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Inventario.Core.Services;
using Messaging.Integration.Drivers;
using Tenant.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace Inventario.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddIdentityServerWrapperServices();
        services.AddTenantWrapperServices();
        services.AddMessagingWrapperServices();
        //services.AddDecoratorHandlers(typeof(IdentityServerCore).Assembly);
        services.AddTenantService();
        
        // Register ProductService
        services.AddScoped<ProductService>();
        
        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<Program>();
        
        // VSA: Command/Query handlers are now registered via InstallStandardServices
        // See: XFramework.Core.Extensions.InstallerExtensions.InstallStandardServices
        // Handlers from IdentityServer.Domain.Shared will be auto-discovered and registered
    }
}