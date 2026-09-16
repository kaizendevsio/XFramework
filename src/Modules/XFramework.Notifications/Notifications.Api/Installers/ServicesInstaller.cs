using Notifications.Api.Services;
using Notifications.Api.Services.Push;
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
        services.AddSingleton<NotificationDeliverySignal>();
        services.AddScoped<WebPushVapidProvider>();
        services.AddScoped<NotificationPushService>();
        services.AddMemoryCache();
        services.AddSingleton<PushPresence>();
        services.AddSingleton<WebPushSender>();
        // Push services are a handful of long-lived hosts; a pooled handler avoids a TLS
        // handshake per notification without pinning DNS for the life of the process.
        services.AddHttpClient(WebPushSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15))
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));
        services.AddHostedService<NotificationDeliveryDispatcherHostedService>();
    }
}
