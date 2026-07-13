using Notifications.Integration.Drivers;
using Storage.Integration.Drivers;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Abstractions.Wrappers;
using XFramework.Integration.Extensions;

namespace Communications.Api.Installers;

public sealed class WrapperInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddXFrameworkBoltClient(configuration, hostEnvironment: hostEnvironment);
        services.AddSingleton<INotificationsServiceWrapper, NotificationsServiceWrapper>();
        services.AddStorageWrapperServices();
    }
}
