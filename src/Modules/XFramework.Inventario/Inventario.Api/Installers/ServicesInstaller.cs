using FluentValidation;
using Communications.Integration.Drivers;
using XFramework.Inventario.Api.Services;
using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace Inventario.Api.Installers;

public class ServicesInstaller : IInstaller
{
    public virtual void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        services.AddCommunicationsWrapperServices();
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();

        // Register ProductService
        services.AddScoped<ProductService>();
        services.AddScoped<StockPostingService>();
        services.AddScoped<WarehouseService>();
        services.AddScoped<ReservationService>();
        services.AddScoped<InventoryAllocationService>();
        services.AddScoped<InventoryLotService>();
        services.AddScoped<InventoryPlanningService>();
        services.AddScoped<InventoryReportingService>();
        services.AddScoped<PurchasingService>();
        services.AddScoped<ProductVariationService>();

        // Register FluentValidation validators
        services.AddValidatorsFromAssemblyContaining<Program>();
    }
}
