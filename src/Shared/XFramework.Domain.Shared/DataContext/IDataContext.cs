namespace XFramework.Domain.Shared.DataContext;

public interface IDataContext
{
    IRemoteQuery<T> Query<T>() where T : class;
    void Add<T>(T entity) where T : class;
    void Update<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default);
}
