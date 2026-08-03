using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Core.DataContext;

public class ServerDataContext<TDbContext>(
    TDbContext dbContext,
    ILogger<ServerDataContext<TDbContext>>? logger = null) : IDataContext
    where TDbContext : DbContext
{
    public IRemoteQuery<T> Query<T>() where T : class
        => new ServerQuery<T>(dbContext.Set<T>());

    public void Add<T>(T entity) where T : class
        => dbContext.Add(entity);

    public void Update<T>(T entity) where T : class
        => dbContext.Update(entity);

    public void Remove<T>(T entity) where T : class
        => dbContext.Remove(entity);

    public async Task<DataContextResult> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
            return DataContextResult.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger?.LogWarning(ex, "Concurrent database update rejected for {DbContextType}", typeof(TDbContext).Name);
            return DataContextResult.Failure("The record was changed by another operation. Reload and try again.", 409);
        }
        catch (DbUpdateException ex)
        {
            logger?.LogError(ex, "Database update failed for {DbContextType}", typeof(TDbContext).Name);
            return DataContextResult.Failure("The database update could not be completed.");
        }
    }
}
