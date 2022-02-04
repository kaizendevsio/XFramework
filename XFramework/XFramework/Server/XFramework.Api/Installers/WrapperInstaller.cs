using XFramework.Integration.Drivers;

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