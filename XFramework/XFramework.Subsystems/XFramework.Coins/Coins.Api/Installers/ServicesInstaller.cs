using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Coins.Core.Interfaces;
using Coins.Core.Services;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;

namespace Coins.Api.Installers
{
    public class ServicesInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ICachingService, CachingService>();
            services.AddSingleton<IHelperService, HelperService>();
            services.AddSingleton<IJwtService, JwtService>();

        }
    }
}