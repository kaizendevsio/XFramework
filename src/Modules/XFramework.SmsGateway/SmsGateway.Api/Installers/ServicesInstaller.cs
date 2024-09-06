using Messaging.Integration.Drivers;
using SmsGateway.Core;
using SmsGateway.Core.Interfaces;
using SmsGateway.Core.Services;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;

namespace SmsGateway.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddSingleton<ICachingService, CachingService>();
        services.AddMessagingWrapperServices();
        
        services.AddMediatR(o => o.RegisterServicesFromAssemblies(
            typeof(SmsGatewayBaseRequest).Assembly,
            typeof(SmsGatewayCore).Assembly
        ));
    }
}