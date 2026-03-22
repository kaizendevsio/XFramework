using IdentityServer.Domain.Shared.Contracts;
using Microsoft.Extensions.Caching.Memory;

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
/// TODO: Re-implement tenant service wrapper when IdentityServer.Api exposes tenant endpoints.
/// </summary>
public sealed class TenantResolver(IMemoryCache cache) : ITenantResolver
{
    /// <inheritdoc />
    public Task<Tenant> GetTenant(Guid? id)
    {
        if (id is null || id == Guid.Empty) throw new ArgumentNullException(nameof(id));

        if (cache.TryGetValue($"GetTenant-{id}", out Tenant? entity) && entity is not null)
        {
            return Task.FromResult(entity);
        }
        
        // TODO: Implement actual tenant retrieval from IdentityServer.Api
        // For now, throw an exception indicating the service needs to be configured
        throw new InvalidOperationException(
            $"Tenant service is not fully configured. Cannot retrieve tenant with id '{id}'. " +
            "Please configure a tenant service wrapper or direct API access.");
    }
}