using Microsoft.EntityFrameworkCore;

namespace XFramework.Core.Services;

public interface ITenantService
{
    public Task<Tenant> GetTenant(Guid? id);
} 

public class TenantService(AppDbContext appDbContext) : ITenantService
{
    public async Task<Tenant> GetTenant(Guid? id)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        var entity = await appDbContext.Tenants
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);

        ArgumentNullException.ThrowIfNull(entity, nameof(Tenant));

        return entity;
    }
}