using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Records.Core.Interfaces;
using Records.Core.Services;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;
using XFramework.Integration.Wrappers;

namespace Records.Api.Installers
{
    public class ServicesInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ICachingService, CachingService>();
            services.AddSingleton<IHelperService, HelperService>();
            services.AddSingleton<IJwtService, JwtService>();
            services.AddSingleton<SignalRService>();
            services.AddSingleton<IStreamFlowWrapper, StreamFlowSignalRWrapper>();
        }
    }
}