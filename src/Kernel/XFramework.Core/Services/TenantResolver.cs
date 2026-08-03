using IdentityServer.Domain.Shared.Contracts;
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

    Task<Tenant> GetTenant(Guid? id, CancellationToken ct) => GetTenant(id);

    /// <summary>Removes a tenant from the local resolver cache after lifecycle changes.</summary>
    void Invalidate(Guid id) { }
}

/// <summary>
/// Implementation of ITenantResolver with memory caching.
/// </summary>
public sealed class TenantResolver(IDataContext dataContext) : ITenantResolver
{
    /// <inheritdoc />
    public Task<Tenant> GetTenant(Guid? id) => GetTenant(id, CancellationToken.None);

    public async Task<Tenant> GetTenant(Guid? id, CancellationToken ct)
    {
        if (id is null || id == Guid.Empty) throw new ArgumentNullException(nameof(id));

        var tenant = await dataContext.Query<Tenant>()
            .NoCache()
            .IgnoreQueryFilters()
            .Where(i => i.Id == id)
            .Where(i => !i.IsDeleted && i.IsEnabled)
            .Where(i => i.AvailabilityDate == null || i.AvailabilityDate <= DateTime.UtcNow)
            .Where(i => i.Expiration == null || i.Expiration > DateTime.UtcNow)
            .FirstOrDefaultAsync(ct);

        if (tenant is null)
        {
            throw new InvalidOperationException($"Tenant '{id}' could not be found.");
        }

        return tenant;
    }

    public void Invalidate(Guid id) { }
}
