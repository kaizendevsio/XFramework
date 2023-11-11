﻿using IdentityServer.Core;
using XFramework.Integration.Extensions;

namespace IdentityServer.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddDecoratorHandlers(typeof(IdentityServerCore).Assembly);
        services.AddIdentityServerWrapperServices();
    }
}