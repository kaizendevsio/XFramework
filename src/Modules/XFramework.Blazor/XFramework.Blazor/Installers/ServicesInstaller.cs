﻿using Address.Integration.Drivers;
using IdentityServer.Integration.Drivers;
using Messaging.Integration.Drivers;
using Microsoft.Extensions.DependencyInjection;
using Registry.Integration.Drivers;
using Wallets.Integration.Drivers;
using XFramework.Blazor.Interfaces;

namespace XFramework.Blazor.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IWebAssemblyHostEnvironment webAssemblyHostEnvironment)
    {
        services.AddIdentityServerWrapperServices();
        services.AddAddressWrapperServices();
        services.AddWalletsWrapperServices();
        services.AddMessagingWrapperServices();
        services.AddRegistryWrapperServices();
    }
}