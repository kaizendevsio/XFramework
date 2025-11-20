using IdentityServer.Integration.Drivers;
using Messaging.Core;
using Messaging.Core.Services;
using Messaging.Integration.Drivers;
using SmsGateway.Integration.Drivers;
using Tenant.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Extensions;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace Messaging.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddTenantWrapperServices();
        services.AddMessagingWrapperServices();
        services.AddSmsGatewayWrapperServices();
        services.AddTenantService();

        // Register MessagingService
        services.AddScoped<IMessagingService, MessagingService>();
    }
}