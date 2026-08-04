using Microsoft.Extensions.DependencyInjection;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext.Cache;
using XFramework.Integration.Security;

namespace XFramework.Integration.DataContext;

public static class RemoteDataContextExtensions
{
    public static IServiceCollection AddRemoteDataContext(
        this IServiceCollection services,
        Action<DataContextOptions>? configure = null)
    {
        var options = new DataContextOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IClientCacheService, ClientCacheService>();
        services.AddSingleton<CacheInvalidationHandler>();

        // Register RemoteDataContext as the inner, CachingDataContext as the decorator
        services.AddScoped<RemoteDataContext>();
        services.AddScoped<IDataContext>(sp =>
        {
            var remote = sp.GetRequiredService<RemoteDataContext>();
            var cache = sp.GetRequiredService<IClientCacheService>();
            var opts = sp.GetRequiredService<DataContextOptions>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachingDataContext>>();
            var invocationContext = sp.GetRequiredService<ITrustedInvocationContextAccessor>();
            return new CachingDataContext(remote, cache, opts, logger, invocationContext);
        });
        services.AddScoped<ICacheControl>(sp => (CachingDataContext)sp.GetRequiredService<IDataContext>());

        return services;
    }
}

public class DataContextOptions
{
    public CachePolicy DefaultCachePolicy { get; set; } = new();

    private readonly Dictionary<Type, CachePolicy> _entityCachePolicies = new();

    public void CachePolicyFor<T>(CachePolicy policy) where T : class
        => _entityCachePolicies[typeof(T)] = policy;

    public CachePolicy GetCachePolicy<T>() where T : class
        => _entityCachePolicies.GetValueOrDefault(typeof(T), DefaultCachePolicy);

    public CachePolicy GetCachePolicy(Type entityType)
        => _entityCachePolicies.GetValueOrDefault(entityType, DefaultCachePolicy);
}
