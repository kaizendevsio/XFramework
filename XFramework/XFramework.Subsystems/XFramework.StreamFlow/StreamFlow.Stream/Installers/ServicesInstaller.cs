using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Core.Interfaces;
using StreamFlow.Core.Services;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;

namespace StreamFlow.Stream.Installers
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