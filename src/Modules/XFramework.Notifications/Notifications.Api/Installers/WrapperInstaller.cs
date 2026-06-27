using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Extensions;
using SmsGateway.Integration.Drivers;

namespace Notifications.Api.Installers;

public sealed class WrapperInstaller : IInstaller
{
    public void InstallServices<TAssembly>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkBoltClient(configuration);
        services.AddSingleton<ISmsGatewayServiceWrapper, SmsGatewayServiceWrapper>();
    }
}
