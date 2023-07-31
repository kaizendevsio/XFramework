using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentGateways.Api.SignalR;
using PaymentGateways.Core.Interfaces;
using PaymentGateways.Core.Services;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;
using XFramework.Integration.Services;

namespace PaymentGateways.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICachingService, CachingService>();
        services.AddSingleton<IHelperService, HelperService>();
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddSingleton<ILoggerWrapper, LoggerService>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<ISignalRService, SignalRWrapper>();

    }
}