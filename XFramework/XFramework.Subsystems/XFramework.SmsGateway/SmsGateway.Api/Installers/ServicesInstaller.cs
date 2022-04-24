﻿using SmsGateway.Api.SignalR;
using SmsGateway.Core.Interfaces;
using SmsGateway.Core.Services;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;
using XFramework.Integration.Services;

namespace SmsGateway.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICachingService, CachingService>();
        services.AddSingleton<IHelperService, HelperService>();
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddTransient<ILoggerWrapper, LoggerService>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<ISignalRService, SignalRWrapper>();
          
    }
}