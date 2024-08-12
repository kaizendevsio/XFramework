using IdentityServer.Integration.Drivers;
using Messaging.Core;
using Messaging.Integration.Drivers;
using SmsGateway.Integration.Drivers;
using Tenant.Integration.Drivers;
using XFramework.Integration.Extensions;

namespace Messaging.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTenantWrapperServices();
        services.AddMessagingWrapperServices();
        services.AddSmsGatewayWrapperServices();
        services.AddTenantService();

        services.AddMediatR(o => o.RegisterServicesFromAssemblies(
            typeof(MessagingBaseRequest).Assembly,
            typeof(MessagingCore).Assembly
        ));
    }
}