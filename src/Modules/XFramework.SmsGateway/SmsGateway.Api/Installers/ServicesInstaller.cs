using SmsGateway.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace SmsGateway.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddTenantModuleFeatures();

        // Register SMS service (VSA migration - replaced MediatR)
        services.AddScoped<ISmsService, SmsService>();
    }
}
