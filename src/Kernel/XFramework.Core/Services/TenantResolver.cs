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
/// Tenant lookup is parked until IdentityServer.Api exposes a tenant lookup contract.
/// </summary>
public sealed class TenantResolver(IMemoryCache cache) : ITenantResolver
{
    private const string UnsupportedTenantLookupMessage =
        "Tenant lookup is not supported by the default TenantResolver. " +
        "Configure a concrete ITenantResolver once IdentityServer.Api exposes a tenant endpoint or service wrapper.";

    /// <inheritdoc />
    public Task<Tenant> GetTenant(Guid? id)
    {
        if (id is null || id == Guid.Empty) throw new ArgumentNullException(nameof(id));

        if (cache.TryGetValue($"GetTenant-{id}", out Tenant? entity) && entity is not null)
        {
            return Task.FromResult(entity);
        }

        throw new NotSupportedException($"{UnsupportedTenantLookupMessage} Tenant id: '{id}'.");
    }
}
