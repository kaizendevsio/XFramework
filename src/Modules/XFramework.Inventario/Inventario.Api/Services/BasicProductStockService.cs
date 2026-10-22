using IdentityServer.Domain.Shared.Contracts;
using Inventario.Api.Features.Stock.PostBasic;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Api.Services;

public sealed class BasicProductStockService(
    DbContext db,
    ITrustedInvocationContextAccessor invocation,
    ITenantModuleFeatureService features,
    StockPostingService stockPosting,
    ILogger<BasicProductStockService> logger)
{
    private const string ReferenceType = "BasicProductStock";

    public async Task<Result<StockPostingResponse>> PostAsync(PostBasicProductStockRequest request, CancellationToken ct = default)
    {
        var context = invocation.Current;
        if (context?.Actor is not { } actor || actor.ExpiresAtUtc <= DateTimeOffset.UtcNow ||
            context.EffectiveTenantId is not { } tenantId || tenantId == Guid.Empty)
            return Result<StockPostingResponse>.Unauthorized("Sign in and select a tenant.");
        if (request.Metadata.RequestedTenantId is { } requested && requested != tenantId)
            return Result<StockPostingResponse>.Forbidden("The requested tenant does not match the authorized tenant.");
        if (!actor.Capabilities.Contains("inventario.movements:create") ||
            (actor.TenantId != tenantId && !actor.Capabilities.Contains(XFrameworkActorCapabilities.IdentityTenantsManage)))
            return Result<StockPostingResponse>.Forbidden("Stock movement permission is required for this tenant.");

        foreach (var feature in new string?[] { null, TenantModuleFeatureKeys.StockBalancesSubFeature,
                     TenantModuleFeatureKeys.MovementsSubFeature, TenantModuleFeatureKeys.WarehousingSubFeature })
        {
            var gate = await features.EnsureEnabledAsync(tenantId, TenantModuleFeatureKeys.Inventario, feature, ct);
            if (!gate.IsSuccess) return Result<StockPostingResponse>.Failure(gate.Message!, gate.StatusCode);
        }
        var validation = await new PostBasicProductStockValidator().ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<StockPostingResponse>.Failure(string.Join(" ", validation.Errors.Select(x => x.ErrorMessage)), 400);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var committed = false;
        try
        {
            // Share setup's transaction lock so concurrent preferences/provisioning see the committed defaults.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"inventario:setup:" + tenantId}, 0))", ct);
            var setup = await db.Set<InventarioSetup>().AsTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted, ct);
            if (setup?.Mode != InventarioSetupMode.Basic)
                return Result<StockPostingResponse>.Conflict("Select Basic inventory mode in Settings before receiving basic stock.");

            var key = request.IdempotencyKey.Trim();
            var previous = await db.Set<InventoryMovement>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.IdempotencyKey == key && !x.IsDeleted, ct);
            Guid warehouseId;
            Guid locationId;
            if (previous is not null)
            {
                if (previous.ReferenceType != ReferenceType || previous.WarehouseId is null || previous.LocationId is null)
                    return Result<StockPostingResponse>.Conflict("Idempotency key was already used with a different request.");
                // Replay against the original destination even if the tenant has since changed its defaults.
                warehouseId = previous.WarehouseId.Value;
                locationId = previous.LocationId.Value;
            }
            else
            {
                var storage = await ResolveStorageAsync(tenantId, setup, ct);
                if (!storage.IsSuccess)
                    return Result<StockPostingResponse>.Failure(storage.Message!, storage.StatusCode);
                (warehouseId, locationId) = storage.Data;
                // StockPosting queries persisted dimensions; this save remains inside the outer transaction.
                await db.SaveChangesAsync(ct);
            }

            var result = await stockPosting.StageAsync(new PostStockMovementRequest
            {
                Metadata = request.Metadata,
                ProductId = request.ProductId,
                ProductVariationId = request.ProductVariationId,
                WarehouseId = warehouseId,
                LocationId = locationId,
                MovementType = InventoryMovementType.Receipt,
                Quantity = request.Quantity,
                Reason = request.Reason,
                ReferenceType = ReferenceType,
                IdempotencyKey = key
            }, ct);
            if (!result.IsSuccess) return result;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            committed = true;
            return result;
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Basic stock receipt conflicted for tenant {TenantId}.", tenantId);
            return Result<StockPostingResponse>.Conflict("Stock or storage changed. Retry with the same idempotency key.");
        }
        finally
        {
            if (!committed)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Result<(Guid WarehouseId, Guid LocationId)>> ResolveStorageAsync(Guid tenantId, InventarioSetup setup, CancellationToken ct)
    {
        InventoryLocation? location = null;
        if (setup.LocationId is { } selectedLocation)
        {
            location = await db.Set<InventoryLocation>().AsNoTracking().SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && x.Id == selectedLocation && !x.IsDeleted && x.IsEnabled && x.IsPickable, ct);
            if (location is null || (setup.WarehouseId is { } selectedWarehouse && location.WarehouseId != selectedWarehouse))
                return Result<(Guid, Guid)>.Conflict("The default stock location is unavailable. Update Settings.");
        }
        var warehouseId = setup.WarehouseId ?? location?.WarehouseId;
        var warehouses = db.Set<Warehouse>().AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled);
        var warehouse = warehouseId is { } id
            ? await warehouses.SingleOrDefaultAsync(x => x.Id == id, ct)
            : await warehouses.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (warehouseId is not null && warehouse is null)
            return Result<(Guid, Guid)>.Conflict("The default stock area is unavailable. Update Settings.");
        if (location is null && warehouse is not null)
            location = await db.Set<InventoryLocation>().AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.WarehouseId == warehouse.Id && !x.IsDeleted && x.IsEnabled && x.IsPickable)
                .OrderBy(x => x.Id).FirstOrDefaultAsync(ct);

        if (setup.WarehouseId is null || setup.LocationId is null)
        {
            if (invocation.Current?.Actor?.Capabilities.Contains(XFrameworkActorCapabilities.IdentityTenantsManage) != true)
                return Result<(Guid, Guid)>.Forbidden("Tenant management permission is required to configure the default stock area.");
            if ((warehouse is null || location is null) && invocation.Current?.Actor?.Capabilities.Contains("inventario.warehousing:create") != true)
                return Result<(Guid, Guid)>.Forbidden("Warehousing create permission is required to provision a stock area.");
            if (warehouse is null)
            {
                warehouse = new Warehouse
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, Code = "STOCK-" + Guid.NewGuid().ToString("N"), Name = "My stock",
                    IsDefault = !await db.Set<Warehouse>().AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted && x.IsDefault, ct),
                    IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid()
                };
                db.Add(warehouse);
            }
            if (location is null)
            {
                location = new InventoryLocation
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id,
                    Code = "STOCK-" + Guid.NewGuid().ToString("N"), Name = "General stock",
                    IsPickable = true, IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid()
                };
                db.Add(location);
            }
            setup.WarehouseId = warehouse.Id;
            setup.LocationId = location.Id;
            setup.ModifiedAt = DateTime.UtcNow;
            setup.ConcurrencyStamp = Guid.NewGuid();
        }
        return Result<(Guid, Guid)>.Success((warehouse!.Id, location!.Id));
    }
}
