using IdentityServer.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Core.Services.Caching;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Brands;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Brands;

namespace XFramework.Inventario.Api.Services;

public sealed class ProductBrandService(
    AppDbContext db,
    ITrustedInvocationContextAccessor invocation,
    ITenantModuleFeatureService features,
    ICacheService cache,
    ILogger<ProductBrandService> logger)
{
    public async Task<Result<List<string>>> SearchAsync(SearchProductBrandsRequest request, CancellationToken ct = default)
    {
        var access = await AccessAsync(request, request.CategoryId, false, ct);
        if (!access.IsSuccess) return Result<List<string>>.Failure(access.Message!, access.StatusCode);
        var search = ProductBrand.Normalize(request.Search);
        if (search?.Length > ProductBrand.MaxLength)
            return Result<List<string>>.Failure("Brand search cannot exceed 100 characters.", 400);

        // Normalize and deduplicate in SQL before limiting, not a truncated product scan.
        var query = Brands(access.Data, request.CategoryId).Select(x => x.NormalizedBrand).Where(x => x != "");
        if (search is not null) query = query.Where(x => x.Contains(search));
        var brands = await query.Distinct().OrderBy(x => x != search).ThenBy(x => x)
            .Take(Math.Clamp(request.Limit, 1, 50)).ToListAsync(ct);
        return Result<List<string>>.Success(brands);
    }

    public async Task<Result<ProductBrandMergePreview>> PreviewAsync(PreviewProductBrandMergeRequest request, CancellationToken ct = default)
    {
        var access = await AccessAsync(request, request.CategoryId, true, ct);
        if (!access.IsSuccess) return Result<ProductBrandMergePreview>.Failure(access.Message!, access.StatusCode);
        return await PreviewCoreAsync(access.Data, request.CategoryId, request.SourceBrand, request.TargetBrand, ct);
    }

    public async Task<Result<ProductBrandMergePreview>> MergeAsync(MergeProductBrandsRequest request, CancellationToken ct = default)
    {
        var access = await AccessAsync(request, request.CategoryId, true, ct);
        if (!access.IsSuccess) return Result<ProductBrandMergePreview>.Failure(access.Message!, access.StatusCode);
        if (!request.Confirmed || request.ExpectedProductCount <= 0)
            return Result<ProductBrandMergePreview>.Failure("Preview and confirm the affected products before merging.", 400);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var preview = await PreviewCoreAsync(access.Data, request.CategoryId, request.SourceBrand, request.TargetBrand, ct);
        if (!preview.IsSuccess) return preview;
        var plan = preview.Data!;
        if (plan.AffectedProductCount != request.ExpectedProductCount)
            return Result<ProductBrandMergePreview>.Conflict("Brands changed. Preview the merge again.");

        // Explicit 'g' is required: Npgsql's Regex.Replace translation replaces only the first run.
        // Keep the predicate on the updated row so PostgreSQL rechecks it after any lock wait.
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Inventario"."Product"
            SET "Brand" = {plan.TargetBrand},
                "ModifiedAt" = {DateTime.UtcNow},
                "ConcurrencyStamp" = {Guid.NewGuid()}
            WHERE "TenantId" = {access.Data}
                AND "CategoryId" = {request.CategoryId}
                AND "IsDeleted" = false
                AND "Brand" IS NOT NULL
                AND "Brand" <> {plan.TargetBrand}
                AND upper(btrim(regexp_replace("Brand", {ProductBrand.WhitespacePattern}, ' ', 'g'))) = {plan.SourceBrand}
            """, ct);
        if (changed != request.ExpectedProductCount)
        {
            await transaction.RollbackAsync(ct);
            return Result<ProductBrandMergePreview>.Conflict("Brands changed. Preview the merge again.");
        }

        await transaction.CommitAsync(ct);
        await InvalidateAsync(access.Data, ct);
        logger.LogInformation("Merged brands in category {CategoryId} for tenant {TenantId}; {ProductCount} products updated.",
            request.CategoryId, access.Data, changed);
        return Result<ProductBrandMergePreview>.Success(plan);
    }

    private async Task<Result<ProductBrandMergePreview>> PreviewCoreAsync(
        Guid tenantId, Guid categoryId, string source, string target, CancellationToken ct)
    {
        var sourceBrand = ProductBrand.Normalize(source);
        var targetBrand = ProductBrand.Normalize(target);
        if (sourceBrand is null || targetBrand is null ||
            sourceBrand.Length > ProductBrand.MaxLength || targetBrand.Length > ProductBrand.MaxLength)
            return Result<ProductBrandMergePreview>.Failure("Choose source and target brands of at most 100 characters.", 400);
        var brands = Brands(tenantId, categoryId);
        if (!await brands.AnyAsync(x => x.NormalizedBrand == targetBrand, ct))
            return Result<ProductBrandMergePreview>.NotFound("The target brand is not used in this category.");
        var count = await brands.CountAsync(x => x.NormalizedBrand == sourceBrand && x.Brand != targetBrand, ct);
        if (count == 0)
            return Result<ProductBrandMergePreview>.Conflict("No products need this merge.");
        return Result<ProductBrandMergePreview>.Success(new(categoryId, sourceBrand, targetBrand, count));
    }

    // Composable, parameterized SQL keeps normalization global before filtering/distinct/limit.
    // SqlQuery has no entity filters, so tenant/category/soft-delete scope is explicit here.
    private IQueryable<BrandRow> Brands(Guid tenantId, Guid categoryId) =>
        db.Database.SqlQuery<BrandRow>($"""
            SELECT "Brand",
                upper(btrim(regexp_replace("Brand", {ProductBrand.WhitespacePattern}, ' ', 'g'))) AS "NormalizedBrand"
            FROM "Inventario"."Product"
            WHERE "TenantId" = {tenantId}
                AND "CategoryId" = {categoryId}
                AND "IsDeleted" = false
                AND "Brand" IS NOT NULL
            """);

    private async Task<Result<Guid>> AccessAsync(RequestBase request, Guid categoryId, bool manage, CancellationToken ct)
    {
        var context = invocation.Current;
        if (context?.Actor is not { } actor || actor.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
            context.EffectiveTenantId is not { } tenantId || tenantId == Guid.Empty)
            return Result<Guid>.Unauthorized("Sign in and select a tenant.");
        if (request.Metadata.RequestedTenantId is { } requested && requested != tenantId)
            return Result<Guid>.Forbidden("The requested tenant does not match the authorized tenant.");
        if ((manage || actor.TenantId != tenantId) &&
            !actor.Capabilities.Contains(XFrameworkActorCapabilities.IdentityTenantsManage))
            return Result<Guid>.Forbidden("Tenant management permission is required for this brand operation.");
        var gate = await features.EnsureEnabledAsync(tenantId, TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.CatalogSubFeature, ct);
        if (!gate.IsSuccess) return Result<Guid>.Failure(gate.Message!, gate.StatusCode);
        if (categoryId == Guid.Empty) return Result<Guid>.Failure("Select a category.", 400);
        if (!await db.Set<ProductCategory>().AnyAsync(x => x.Id == categoryId && x.TenantId == tenantId && !x.IsDeleted, ct))
            return Result<Guid>.NotFound("Product category not found.");
        return Result<Guid>.Success(tenantId);
    }

    private async Task InvalidateAsync(Guid tenantId, CancellationToken ct)
    {
        // Merge may touch many products; invalidate only this tenant's product item/list caches.
        foreach (var prefix in new[] { $"inventario:tenant:{tenantId}:product:", $"inventario:tenant:{tenantId}:products:" })
        {
            try
            {
                var result = await cache.RemoveByPrefixAsync(prefix, ct);
                if (!result.IsSuccess) logger.LogWarning("Brand merge cache invalidation failed for {CachePrefix}.", prefix);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Brand merge cache invalidation failed for {CachePrefix}.", prefix);
            }
        }
    }

    private sealed class BrandRow
    {
        public string Brand { get; init; } = "";
        public string NormalizedBrand { get; init; } = "";
    }
}

