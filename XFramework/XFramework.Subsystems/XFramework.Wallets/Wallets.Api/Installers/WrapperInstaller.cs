/*using LoadManna.Integration.Drivers;
using LoadManna.Integration.Interfaces.Wrappers;*/

using Wallets.Api.SignalR;
using Wallets.Core.Services;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace Wallets.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddTransient<ILoggerWrapper, LoggerService>();
        services.AddSingleton<ISignalRService, SignalRWrapper>();
        /*services.AddSingleton<IPaymentGatewayWrapper, PaymentGatewayDriver>();*/
    }
}