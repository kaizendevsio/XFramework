using XFramework.Domain.Shared.DataContext;

namespace XFramework.Integration.DataContext;

/// <summary>
/// Remote data context that proxies EF Core queries and changes through the Bolt hub.
/// DB proxy migration to the Bolt thin protocol is parked work (see Task 14).
/// </summary>
public class RemoteDataContext : IDataContext
{
    private const string PendingMigrationMessage =
        "DB proxy migration to Bolt thin protocol is pending — see DB proxy decentralization parked work (Task 14).";

    public RemoteDataContext()
    {
    }

    public IRemoteQuery<T> Query<T>() where T : class
    {
        throw new NotImplementedException(PendingMigrationMessage);
    }

    public void Add<T>(T entity) where T : class
    {
        throw new NotImplementedException(PendingMigrationMessage);
    }

    public void Update<T>(T entity) where T : class
    {
        throw new NotImplementedException(PendingMigrationMessage);
    }

    public void Remove<T>(T entity) where T : class
    {
        throw new NotImplementedException(PendingMigrationMessage);
    }

    public Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException(PendingMigrationMessage);
    }
}
