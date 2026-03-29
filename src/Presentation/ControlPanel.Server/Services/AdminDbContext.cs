using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Contexts;

namespace ControlPanel.Server.Services;

/// <summary>
/// Provides admin-level database access that bypasses the global tenant query filter.
/// Creates a fresh AppDbContext per operation via AdminDbContextFactory to avoid
/// the "second operation started" concurrency error in Blazor Server.
/// </summary>
public class AdminDbContext(AdminDbContextFactory factory)
{
    private AppDbContext? _writeContext;

    /// <summary>
    /// Query entities with NO tenant filter (sees all tenants).
    /// Each call creates a fresh DbContext — safe for concurrent use.
    /// </summary>
    public IQueryable<T> Query<T>() where T : class
    {
        var db = factory.Create();
        return db.Set<T>().IgnoreQueryFilters();
    }

    public void Add<T>(T entity) where T : class
    {
        _writeContext ??= factory.Create();
        _writeContext.Add(entity);
    }

    public void Update<T>(T entity) where T : class
    {
        _writeContext ??= factory.Create();
        _writeContext.Update(entity);
    }

    public void Remove<T>(T entity) where T : class
    {
        _writeContext ??= factory.Create();
        _writeContext.Remove(entity);
    }

    /// <summary>
    /// Persist all staged changes (Add/Update/Remove) to the database.
    /// </summary>
    public async Task<(bool Success, string? Error)> SaveAsync(CancellationToken ct = default)
    {
        if (_writeContext is null)
            return (true, null);

        try
        {
            await _writeContext.SaveChangesAsync(ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            await _writeContext.DisposeAsync();
            _writeContext = null;
        }
    }
}
