﻿using IdentityServer.Api.SignalR;
using IdentityServer.Core.Services;
using IdentityServer.Integration.Drivers;
using IdentityServer.Integration.Interfaces;
using Messaging.Integration.Drivers;
using Messaging.Integration.Interfaces;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace IdentityServer.Api.Installers;

public class WrapperInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageBusWrapper, StreamFlowDriverSignalR>();
        services.AddTransient<ILoggerWrapper, LoggerService>();
        services.AddSingleton<ISignalRService, SignalRWrapper>();
        services.AddSingleton<IIdentityServiceWrapper, IdentityServerDriver>();
        services.AddSingleton<IMessagingServiceWrapper, MessagingServiceDriver>();
    }
}