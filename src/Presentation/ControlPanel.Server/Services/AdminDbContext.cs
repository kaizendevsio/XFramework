using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Contexts;

namespace ControlPanel.Server.Services;

/// <summary>
/// Provides admin-level database access that bypasses the global tenant query filter.
/// Use this instead of IDataContext when you need to see data across ALL tenants.
/// </summary>
public class AdminDbContext(AppDbContext dbContext)
{
    /// <summary>
    /// Query entities with NO tenant filter (sees all tenants).
    /// </summary>
    public IQueryable<T> Query<T>() where T : class
        => dbContext.Set<T>().IgnoreQueryFilters();

    /// <summary>
    /// Query entities with tenant filter intact (respects tenant scope).
    /// </summary>
    public IQueryable<T> QueryScoped<T>() where T : class
        => dbContext.Set<T>();

    public void Add<T>(T entity) where T : class
        => dbContext.Add(entity);

    public void Update<T>(T entity) where T : class
        => dbContext.Update(entity);

    public void Remove<T>(T entity) where T : class
        => dbContext.Remove(entity);

    public async Task<(bool Success, string? Error)> SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.InnerException?.Message ?? ex.Message);
        }
    }
}
