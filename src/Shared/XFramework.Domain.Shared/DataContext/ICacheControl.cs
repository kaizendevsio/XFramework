namespace XFramework.Domain.Shared.DataContext;

public interface ICacheControl
{
    Task InvalidateAsync<T>(CancellationToken ct = default) where T : class;
    Task InvalidateAsync<T>(Guid id, CancellationToken ct = default) where T : class;
    Task PrefetchAsync<T>(IRemoteQuery<T> query, CancellationToken ct = default) where T : class;
    Task ClearAllAsync(CancellationToken ct = default);
}
