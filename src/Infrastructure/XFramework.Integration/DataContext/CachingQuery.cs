using System.Linq.Expressions;
using XFramework.Domain.Shared.DataContext;
using XFramework.Integration.DataContext.Cache;

namespace XFramework.Integration.DataContext;

public class CachingQuery<T> : IRemoteQuery<T> where T : class
{
    private readonly IRemoteQuery<T> _inner;
    private readonly IClientCacheService _cache;
    private readonly CachePolicy _policy;
    private bool _noCache;

    public CachingQuery(IRemoteQuery<T> inner, IClientCacheService cache, CachePolicy policy)
    {
        _inner = inner;
        _cache = cache;
        _policy = policy;
    }

    // All builder methods delegate to inner and return this wrapper
    public IRemoteQuery<T> Where(Expression<Func<T, bool>> predicate) { _inner.Where(predicate); return this; }
    public IRemoteQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector) { _inner.OrderBy(keySelector); return this; }
    public IRemoteQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector) { _inner.OrderByDescending(keySelector); return this; }
    public IRemoteQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector) { _inner.ThenBy(keySelector); return this; }
    public IRemoteQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector) { _inner.ThenByDescending(keySelector); return this; }
    public IRemoteQuery<T> Skip(int count) { _inner.Skip(count); return this; }
    public IRemoteQuery<T> Take(int count) { _inner.Take(count); return this; }
    public IRemoteQuery<T> Include<TProperty>(Expression<Func<T, TProperty>> navigationSelector) { _inner.Include(navigationSelector); return this; }
    public IRemoteQuery<T> Distinct() { _inner.Distinct(); return this; }
    public IRemoteQuery<T> DistinctBy<TKey>(Expression<Func<T, TKey>> keySelector) { _inner.DistinctBy(keySelector); return this; }
    public IRemoteQuery<T> NoCache() { _noCache = true; _inner.NoCache(); return this; }

    public async Task<List<T>> ToListAsync(CancellationToken ct = default)
    {
        if (ShouldUseCache)
        {
            var key = GetCacheKey();
            var cached = await _cache.GetAsync<List<T>>(key, ct);
            if (cached is not null) return cached;

            var result = await _inner.ToListAsync(ct);
            await _cache.SetAsync(key, result, _policy.AbsoluteExpiration, ct);
            return result;
        }

        return await _inner.ToListAsync(ct);
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken ct = default)
    {
        if (ShouldUseCache)
        {
            var key = GetCacheKey();
            var cached = await _cache.GetAsync<T>(key, ct);
            if (cached is not null) return cached;

            var result = await _inner.FirstOrDefaultAsync(ct);
            if (result is not null)
                await _cache.SetAsync(key, result, _policy.AbsoluteExpiration, ct);
            return result;
        }

        return await _inner.FirstOrDefaultAsync(ct);
    }

    public async Task<T?> SingleOrDefaultAsync(CancellationToken ct = default)
    {
        if (ShouldUseCache)
        {
            var key = GetCacheKey();
            var cached = await _cache.GetAsync<T>(key, ct);
            if (cached is not null) return cached;

            var result = await _inner.SingleOrDefaultAsync(ct);
            if (result is not null)
                await _cache.SetAsync(key, result, _policy.AbsoluteExpiration, ct);
            return result;
        }

        return await _inner.SingleOrDefaultAsync(ct);
    }

    // Streaming bypasses cache — results are yielded one by one
    public IAsyncEnumerable<T> ToAsyncEnumerable(CancellationToken ct = default)
        => _inner.ToAsyncEnumerable(ct);

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        if (ShouldUseCache)
        {
            var key = GetCacheKey();
            var cached = await _cache.GetAsync<int?>(key, ct);
            if (cached.HasValue) return cached.Value;

            var result = await _inner.CountAsync(ct);
            await _cache.SetAsync<int?>(key, result, _policy.AbsoluteExpiration, ct);
            return result;
        }

        return await _inner.CountAsync(ct);
    }

    // Scalar operations — low overhead, skip caching
    public Task<bool> AnyAsync(CancellationToken ct = default) => _inner.AnyAsync(ct);
    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => _inner.AnyAsync(predicate, ct);
    public Task<bool> AllAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => _inner.AllAsync(predicate, ct);
    public Task<TResult?> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) => _inner.MinAsync(selector, ct);
    public Task<TResult?> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default) => _inner.MaxAsync(selector, ct);
    public Task<T?> MinByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) => _inner.MinByAsync(keySelector, ct);
    public Task<T?> MaxByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) => _inner.MaxByAsync(keySelector, ct);
    public Task<decimal> SumAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) => _inner.SumAsync(selector, ct);
    public Task<double> AverageAsync(Expression<Func<T, decimal>> selector, CancellationToken ct = default) => _inner.AverageAsync(selector, ct);
    public Task<List<GroupResult<TKey, T>>> GroupByAsync<TKey>(Expression<Func<T, TKey>> keySelector, CancellationToken ct = default) => _inner.GroupByAsync(keySelector, ct);

    private bool ShouldUseCache => _policy.Enabled && !_noCache;

    private string GetCacheKey()
    {
        if (_inner is RemoteQuery<T> remoteQuery)
            return CacheKeyBuilder.ForQuery<T>(remoteQuery.Descriptor);

        // Fallback for non-remote queries
        return $"{typeof(T).Name}:query:{Guid.NewGuid()}";
    }
}
