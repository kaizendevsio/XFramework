using Storage.Api.Services.Providers;
using Storage.Api.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace Storage.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TAssembly>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();
        XFramework.GeneratedServices.GeneratedEntityServiceRegistrations
            .AddGeneratedEntityServices(services);
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddScoped<StorageService>();
        services.AddScoped<StorageMaintenanceService>();
        services.AddHostedService<StorageMaintenanceHostedService>();
        services.AddSingleton<IStorageProviderFactory, StorageProviderFactory>();
        services.AddSingleton<AzureBlobStorageProvider>();
        services.AddSingleton<S3CompatibleStorageProvider>();
        services.AddHealthChecks().AddCheck<StorageProviderReadinessHealthCheck>(
            "Storage-object-provider",
            HealthStatus.Unhealthy,
            ["storage-provider", "ready"]);
    }
}
