﻿namespace HealthEssentials.Api.Installers;

public class HostedServiceInstaller : IInstaller
{
    public void InstallServices(IServiceCollection services, IConfiguration configuration)
    {
        //services.AddHostedService<ProcessMonitorHostedService>();
    }
}