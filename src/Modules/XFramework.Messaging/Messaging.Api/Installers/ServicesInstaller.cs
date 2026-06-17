using SmsGateway.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace Messaging.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Register SmsGateway service wrapper
        services.AddSingleton<ISmsGatewayServiceWrapper, SmsGatewayServiceWrapper>();
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();

        // Register MessagingService
        services.AddScoped<IMessagingService, MessagingService>();

        // Register ThreadService
        services.AddScoped<IThreadService, ThreadService>();
    }
}
