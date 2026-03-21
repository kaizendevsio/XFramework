using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Core.DataContext;

public class ServerDataContext<TDbContext>(TDbContext dbContext) : IDataContext
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
        catch (DbUpdateException ex)
        {
            return DataContextResult.Failure(ex.InnerException?.Message ?? ex.Message);
        }
    }
}
