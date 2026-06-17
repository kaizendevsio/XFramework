using FluentValidation;
using Messaging.Integration.Drivers;
using XFramework.Inventario.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace Inventario.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddMessagingWrapperServices();
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();

        // Register ProductService
        services.AddScoped<ProductService>();

        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<Program>();
    }
}
