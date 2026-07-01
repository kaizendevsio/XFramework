using POS.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace POS.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TAssembly>(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();
        services.AddScoped<PosRegisterService>();
        services.AddScoped<PosCatalogService>();
        services.AddScoped<PosCartService>();
        services.AddScoped<PosSalesService>();
        services.AddScoped<PosReturnsService>();
    }
}
