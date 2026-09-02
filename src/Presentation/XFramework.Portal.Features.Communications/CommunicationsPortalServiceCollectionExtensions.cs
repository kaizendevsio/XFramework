using Microsoft.Extensions.DependencyInjection;
using XFramework.Portal.Features.Communications.Services;

namespace XFramework.Portal.Features.Communications;

public static class CommunicationsPortalServiceCollectionExtensions
{
    public static IServiceCollection AddCommunicationsPortalFeature(this IServiceCollection services)
    {
        services.AddScoped<CommunicationsPortalGuard>();
        services.AddScoped<CommunicationsPortalReadService>();
        services.AddScoped<CommunicationsPortalSettingsService>();
        services.AddScoped<CommunicationsPortalTemplateService>();
        return services;
    }
}
