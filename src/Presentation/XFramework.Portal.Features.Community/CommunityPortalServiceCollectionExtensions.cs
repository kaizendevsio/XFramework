using Microsoft.Extensions.DependencyInjection;
using XFramework.Portal.Features.Community.Services;

namespace XFramework.Portal.Features.Community;

public static class CommunityPortalServiceCollectionExtensions
{
    public static IServiceCollection AddCommunityPortalFeature(this IServiceCollection services)
    {
        services.AddScoped<CommunityPortalAccessService>();
        return services;
    }
}
