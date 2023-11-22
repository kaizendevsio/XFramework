using Wallets.Core;
using XFramework.Integration.Extensions;

namespace Wallets.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddDecoratorHandlers(typeof(WalletsCore).Assembly);
    }
}