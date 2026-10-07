using XFramework.Domain.Shared.Interfaces;
using XFramework.Inventario.Api.Services;

namespace Inventario.Api.Installers;

public sealed class InventarioSetupInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment) =>
        services.AddScoped<InventarioSetupService>();
}
