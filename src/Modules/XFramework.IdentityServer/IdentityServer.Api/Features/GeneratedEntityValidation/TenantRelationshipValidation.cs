using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.Contracts.Base;

namespace IdentityServer.Api.Features.GeneratedEntityValidation;

internal static class TenantRelationshipValidation
{
    internal enum TenantScope
    {
        TenantOnly,
        TenantOrGlobal
    }

    public static Task<bool> ExistsAsync<TEntity>(
        DbContext dbContext,
        Guid id,
        Guid tenantId,
        TenantScope tenantScope,
        CancellationToken ct)
        where TEntity : BaseModel =>
        dbContext.Set<TEntity>()
            .IgnoreQueryFilters()
            .AnyAsync(
                entity => entity.Id == id
                          && (entity.TenantId == tenantId
                              || (tenantScope == TenantScope.TenantOrGlobal
                                  && entity.TenantId == Guid.Empty))
                          && !entity.IsDeleted
                          && entity.IsEnabled,
                ct);

    public static Task<bool> OptionalExistsAsync<TEntity>(
        DbContext dbContext,
        Guid? id,
        Guid tenantId,
        TenantScope tenantScope,
        CancellationToken ct)
        where TEntity : BaseModel =>
        id.HasValue
            ? ExistsAsync<TEntity>(dbContext, id.Value, tenantId, tenantScope, ct)
            : Task.FromResult(true);
}
