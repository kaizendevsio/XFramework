using IdentityServer.Integration.Drivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Tenant.Integration.Drivers;

namespace XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts;

public interface ITenantService
{
    public Task<Tenant> GetTenant(Guid? id);
} 

public class TenantService(ITenantServiceWrapper tenantService, IMemoryCache cache) : ITenantService
{
    public async Task<Tenant> GetTenant(Guid? id)
    {
        if (id is null || id == Guid.Empty) throw new ArgumentNullException(nameof(id));

        if (!cache.TryGetValue($"GetTenant-{id}", out Tenant? entity))
        {
            var response = await tenantService.Tenant.Get(id.Value);
            entity = response.Response;
        }
        
        if (entity is null)
        {
            throw new Exception($"Tenant with id '{id}' not found");
        }
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            // Set the cache expiration as needed
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
        };

        cache.Set($"GetTenant-{id}", entity, cacheOptions);

        return entity;
    }
}