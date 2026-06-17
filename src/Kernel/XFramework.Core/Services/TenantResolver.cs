using IdentityServer.Domain.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Core.Services;

/// <summary>
/// Service for retrieving tenant information with caching support.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Gets a tenant by ID.
    /// </summary>
    /// <param name="id">The tenant ID.</param>
    /// <returns>The tenant entity.</returns>
    Task<Tenant> GetTenant(Guid? id);
}

/// <summary>
/// Implementation of ITenantResolver with memory caching.
/// </summary>
public sealed class TenantResolver(
    IDataContext dataContext,
    IMemoryCache cache) : ITenantResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public async Task<Tenant> GetTenant(Guid? id)
    {
        if (id is null || id == Guid.Empty) throw new ArgumentNullException(nameof(id));

        var cacheKey = $"GetTenant-{id}";
        if (cache.TryGetValue(cacheKey, out Tenant? entity) && entity is not null)
        {
            return entity;
        }

        var tenant = await dataContext.Query<Tenant>()
            .IgnoreQueryFilters()
            .Where(i => i.Id == id)
            .FirstOrDefaultAsync();

        if (tenant is null)
        {
            throw new InvalidOperationException($"Tenant '{id}' could not be found.");
        }

        cache.Set(cacheKey, tenant, CacheDuration);
        return tenant;
    }
}
