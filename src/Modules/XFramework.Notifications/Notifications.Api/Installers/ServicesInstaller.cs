using Notifications.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace Notifications.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TAssembly>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();
        services.AddScoped<NotificationService>();
        services.AddScoped<NotificationDeliveryDispatcher>();
        services.AddHostedService<NotificationDeliveryDispatcherHostedService>();
    }
}
