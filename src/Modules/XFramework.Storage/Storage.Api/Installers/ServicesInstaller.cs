using Storage.Api.Services.Providers;
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
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddScoped<StorageService>();
        services.AddScoped<IStorageProviderFactory, StorageProviderFactory>();
        services.AddScoped<AzureBlobStorageProvider>();
        services.AddScoped<S3CompatibleStorageProvider>();
    }
}
