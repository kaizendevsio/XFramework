using XFramework.Core.Extensions;
using XFramework.Domain.Shared.Interfaces;

namespace Community.Api.Installers;

public sealed class ServicesInstaller : IInstaller
{
    public void InstallServices<TApp>(IServiceCollection services, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        /*services.AddSingleton<ICachingService, CachingService>();*/
        services.AddTenantResolver();
        services.AddTenantModuleFeatures();
        
        // Register Community Services (VSA Architecture)
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddScoped<IContentService, ContentService>();
        services.AddScoped<IConnectionService, ConnectionService>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<INotificationService, NotificationService>();
    }
}
