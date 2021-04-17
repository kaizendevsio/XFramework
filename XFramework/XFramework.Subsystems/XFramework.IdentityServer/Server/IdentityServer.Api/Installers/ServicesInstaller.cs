﻿using IdentityServer.Core.Interfaces;
using IdentityServer.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XFramework.Integration.Drivers;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services;
using XFramework.Integration.Wrappers;

namespace IdentityServer.Api.Installers
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