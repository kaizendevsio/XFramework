using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Core.Interfaces;
using XFramework.Core.Services;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;
using XFramework.Integration.Services;

namespace XFramework.Api.Installers
{
    public class ServicesInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ICachingService, CachingService>();
            services.AddSingleton<IHelperService, HelperService>();
            services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
            services.AddSingleton<ILoggerWrapper, RecordsDriver>();
            services.AddSingleton<IJwtService, JwtService>();
            services.AddSingleton<SignalRService>();
          
        }
    }
}