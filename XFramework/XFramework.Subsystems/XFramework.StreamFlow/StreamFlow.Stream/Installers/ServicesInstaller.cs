﻿using StreamFlow.Core.Services;
using Tenant.Integration.Drivers;
using ICachingService = StreamFlow.Core.Interfaces.ICachingService;

namespace StreamFlow.Stream.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ICachingService, CachingService>();
    }
}