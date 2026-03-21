using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext.Cache;

namespace XFramework.Integration.DataContext;

public class CachingDataContext : IDataContext, ICacheControl
{
    private readonly IDataContext _inner;
    private readonly IClientCacheService _cache;
    private readonly DataContextOptions _options;
    private readonly List<string> _affectedEntityTypes = [];

    public CachingDataContext(IDataContext inner, IClientCacheService cache, DataContextOptions options)
    {
        _inner = inner;
        _cache = cache;
        _options = options;
    }

    public IRemoteQuery<T> Query<T>() where T : class
    {
        var innerQuery = _inner.Query<T>();
        var policy = _options.GetCachePolicy<T>();
        return new CachingQuery<T>(innerQuery, _cache, policy);
    }

    public void Add<T>(T entity) where T : class
    {
        _inner.Add(entity);
        TrackAffectedType<T>();
    }

    public void Update<T>(T entity) where T : class
    {
        _inner.Update(entity);
        TrackAffectedType<T>();
    }

    public void Remove<T>(T entity) where T : class
    {
        _inner.Remove(entity);
        TrackAffectedType<T>();
    }

    public async Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default)
    {
        var result = await _inner.SaveChangesAsync(ct);

        if (result.IsSuccess)
        {
            // Invalidate cache for all affected entity types
            foreach (var entityType in _affectedEntityTypes)
            {
                await _cache.RemoveByPrefixAsync(CacheKeyBuilder.PrefixForEntity(entityType), ct);
            }
            _affectedEntityTypes.Clear();
        }

        return result;
    }

    public async Task InvalidateAsync<T>(CancellationToken ct = default) where T : class
        => await _cache.RemoveByPrefixAsync(CacheKeyBuilder.PrefixForEntity<T>(), ct);

    public async Task InvalidateAsync<T>(Guid id, CancellationToken ct = default) where T : class
        => await _cache.RemoveByPrefixAsync($"{typeof(T).Name}:id:{id}", ct);

    public async Task PrefetchAsync<T>(IRemoteQuery<T> query, CancellationToken ct = default) where T : class
        => await query.ToListAsync(ct); // The CachingQuery wrapper will cache the result

    public async Task ClearAllAsync(CancellationToken ct = default)
        => await _cache.ClearAllAsync(ct);

    private void TrackAffectedType<T>()
    {
        var name = typeof(T).Name;
        if (!_affectedEntityTypes.Contains(name))
            _affectedEntityTypes.Add(name);
    }
}
