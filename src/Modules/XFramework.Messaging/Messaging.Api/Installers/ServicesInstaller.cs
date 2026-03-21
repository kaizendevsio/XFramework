using Messaging.Api.Services;
using SmsGateway.Domain.Shared.Drivers;
using XFramework.Core.Extensions;
using XFramework.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace Messaging.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        // Register SmsGateway service wrapper
        services.AddSingleton<ISmsGatewayServiceWrapper, SmsGatewayServiceWrapper>();
        services.AddTenantService();

        // Register MessagingService
        services.AddScoped<IMessagingService, MessagingService>();
    }
}