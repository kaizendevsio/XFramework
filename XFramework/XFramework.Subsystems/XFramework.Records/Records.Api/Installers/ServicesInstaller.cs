using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Records.Api.SignalR;
using Records.Core.Interfaces;
using Records.Core.Services;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;
using XFramework.Integration.Services;

namespace Records.Api.Installers
{
    public class ServicesInstaller : IInstaller
    {
        public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<ICachingService, CachingService>();
            services.AddSingleton<IHelperService, HelperService>();
            services.AddSingleton<IJwtService, JwtService>();
            services.AddSingleton<ISignalRService, SignalRWrapper>();
            services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        }
    }
}