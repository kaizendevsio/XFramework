using Messaging.Integration.Drivers;
using SmsGateway.Core.Interfaces;
using SmsGateway.Core.Services;
using XFramework.Integration.Extensions;

namespace SmsGateway.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICachingService, CachingService>();
        services.AddMessagingWrapperServices();
    }
}