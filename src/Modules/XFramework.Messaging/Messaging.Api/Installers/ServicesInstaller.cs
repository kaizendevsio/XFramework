using SmsGateway.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace Messaging.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();
    }
}
