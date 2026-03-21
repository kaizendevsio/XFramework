namespace XFramework.Integration.DataContext.Cache;

public interface IClientCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
    Task ClearAllAsync(CancellationToken ct = default);
}
