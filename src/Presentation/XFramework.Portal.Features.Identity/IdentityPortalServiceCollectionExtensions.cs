using Microsoft.Extensions.DependencyInjection;
using XFramework.Portal.Features.Identity.Services;

namespace XFramework.Portal.Features.Identity;

public static class IdentityPortalServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityPortalFeature(this IServiceCollection services)
    {
        services.AddScoped<TenantModuleFeatureDefinitionResolver>();
        return services;
    }
}
