using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Contexts;

namespace ControlPanel.Server.Services;

/// <summary>
/// Provides admin-level database access that bypasses the global tenant query filter.
/// Uses IDbContextFactory to create a fresh DbContext per operation, avoiding the
/// "second operation started" concurrency error in Blazor Server.
/// </summary>
public class AdminDbContext(IDbContextFactory<AppDbContext> factory)
{
    // Each operation gets its own short-lived DbContext from the factory.
    // This prevents concurrent access issues in Blazor Server where
    // the layout and page components share the same scoped DbContext.

    private AppDbContext? _writeContext;

    /// <summary>
    /// Query entities with NO tenant filter (sees all tenants).
    /// Each call creates a fresh DbContext — safe for concurrent use.
    /// </summary>
    public IQueryable<T> Query<T>() where T : class
    {
        var db = factory.CreateDbContext();
        return db.Set<T>().IgnoreQueryFilters();
    }

    /// <summary>
    /// Stage an entity for insertion. Call SaveAsync() to persist.
    /// </summary>
    public void Add<T>(T entity) where T : class
    {
        _writeContext ??= factory.CreateDbContext();
        _writeContext.Add(entity);
    }

    /// <summary>
    /// Stage an entity for update. Call SaveAsync() to persist.
    /// </summary>
    public void Update<T>(T entity) where T : class
    {
        _writeContext ??= factory.CreateDbContext();
        _writeContext.Update(entity);
    }

    /// <summary>
    /// Stage an entity for removal. Call SaveAsync() to persist.
    /// </summary>
    public void Remove<T>(T entity) where T : class
    {
        _writeContext ??= factory.CreateDbContext();
        _writeContext.Remove(entity);
    }

    /// <summary>
    /// Persist all staged changes (Add/Update/Remove) to the database.
    /// Uses a dedicated DbContext separate from query operations.
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
