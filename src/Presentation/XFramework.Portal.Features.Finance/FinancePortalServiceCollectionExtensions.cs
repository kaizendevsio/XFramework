using Microsoft.Extensions.DependencyInjection;
using XFramework.Portal.Features.Finance.Services;

namespace XFramework.Portal.Features.Finance;

public static class FinancePortalServiceCollectionExtensions
{
    public static IServiceCollection AddFinancePortalFeature(this IServiceCollection services)
    {
        services.AddScoped<WalletsAdminBackendContractService>();
        services.AddScoped<WalletsPortalDisplayService>();
        return services;
    }
}
