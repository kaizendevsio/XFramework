using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Core.Interfaces;
using XFramework.Core.Services;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;
using XFramework.Integration.Wrappers;

namespace XFramework.Api.Installers
{
    public class ServicesInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ICachingService, CachingService>();
            services.AddSingleton<IHelperService, HelperService>();
            services.AddSingleton<IStreamFlowWrapper, StreamFlowSignalRWrapper>();
            services.AddSingleton<IRecordsWrapper, RecordsWrapper>();
            services.AddSingleton<IJwtService, JwtService>();
            services.AddSingleton<SignalRService>();
          
        }
    }
}