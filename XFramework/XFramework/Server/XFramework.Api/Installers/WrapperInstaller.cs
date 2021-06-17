using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Api.Installers
{
    public class WrapperInstaller : IInstaller
    {
        public void InstallServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IIdentityServiceWrapper, IdentityServerDriver>();
            services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
            //services.AddSingleton<ILoggerWrapper, RecordsDriver>();
            services.AddSingleton<IWalletServiceWrapper, WalletServiceDriver>();
        }
    }
}