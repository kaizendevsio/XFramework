using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Locations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Warehouses;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Api.Services;

using XFramework.Integration.Security;

public sealed class WarehouseService(
    IDataContext dataContext,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
    ITenantModuleFeatureService featureService)
{
    public async Task<Result<List<Warehouse>>> GetWarehousesAsync(
        GetWarehousesRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<Warehouse>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureWarehousingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<Warehouse>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var query = dataContext.Query<Warehouse>()
            .Where(x => x.TenantId == tenantResult.Data && !x.IsDeleted);

        if (request.Id is { } id)
            query = query.Where(x => x.Id == id);

        var warehouses = await query
            .OrderBy(x => x.Code)
            .Take(200)
            .ToListAsync(ct);

        return Result<List<Warehouse>>.Success(warehouses);
    }

    public async Task<Result<Warehouse>> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<Warehouse>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureWarehousingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<Warehouse>.Failure(featureResult.Message!, featureResult.StatusCode);

        var tenantId = tenantResult.Data;
        var code = NormalizeRequired(request.Code);
        var name = NormalizeRequired(request.Name);
        if (code is null || name is null)
            return Result<Warehouse>.Failure("Warehouse code and name are required.", 400);

        var duplicate = await dataContext.Query<Warehouse>()
            .AnyAsync(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted, ct);
        if (duplicate)
            return Result<Warehouse>.Failure("A warehouse with the same code already exists.", 409);

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            Description = NormalizeOptional(request.Description),
            AddressLine = NormalizeOptional(request.AddressLine),
            City = NormalizeOptional(request.City),
            Region = NormalizeOptional(request.Region),
            PostalCode = NormalizeOptional(request.PostalCode),
            CountryCode = NormalizeOptional(request.CountryCode),
            IsDefault = request.IsDefault,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(warehouse);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<Warehouse>.Failure(saveResult.Message ?? "Warehouse save failed.", saveResult.StatusCode);

        return Result<Warehouse>.Success(warehouse, 201, "Warehouse created.");
    }

    public async Task<Result<List<InventoryLocation>>> GetLocationsAsync(
        GetInventoryLocationsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<InventoryLocation>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureWarehousingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<InventoryLocation>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var query = dataContext.Query<InventoryLocation>()
            .Where(x => x.TenantId == tenantResult.Data && !x.IsDeleted);

        if (request.WarehouseId is { } id)
            query = query.Where(x => x.WarehouseId == id);

        if (request.Id is { } locationId)
            query = query.Where(x => x.Id == locationId);

        var locations = await query
            .OrderBy(x => x.Code)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<InventoryLocation>>.Success(locations);
    }

    public async Task<Result<InventoryLocation>> CreateLocationAsync(CreateInventoryLocationRequest request, CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<InventoryLocation>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureWarehousingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<InventoryLocation>.Failure(featureResult.Message!, featureResult.StatusCode);

        var tenantId = tenantResult.Data;
        var code = NormalizeRequired(request.Code);
        var name = NormalizeRequired(request.Name);
        if (code is null || name is null)
            return Result<InventoryLocation>.Failure("Location code and name are required.", 400);

        var warehouseExists = await dataContext.Query<Warehouse>()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == request.WarehouseId && !x.IsDeleted, ct);
        if (!warehouseExists)
            return Result<InventoryLocation>.NotFound("Warehouse not found.");

        var duplicate = await dataContext.Query<InventoryLocation>()
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.WarehouseId == request.WarehouseId &&
                x.Code == code &&
                !x.IsDeleted,
                ct);
        if (duplicate)
            return Result<InventoryLocation>.Failure("A location with the same code already exists in this warehouse.", 409);

        var location = new InventoryLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = request.WarehouseId,
            ParentLocationId = request.ParentLocationId,
            Code = code,
            Name = name,
            Description = NormalizeOptional(request.Description),
            LocationType = request.LocationType,
            IsPickable = request.IsPickable,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(location);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<InventoryLocation>.Failure(saveResult.Message ?? "Location save failed.", saveResult.StatusCode);

        return Result<InventoryLocation>.Success(location, 201, "Location created.");
    }

    private Result<Guid> GetCurrentTenantId(RequestBase? request)
    {
        var tenantId = trustedInvocationContextAccessor.Current?.EffectiveTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result<Guid>.Unauthorized("Authentication is required for warehouse operations.");
        return Result<Guid>.Success(tenantId.Value);
    }

    private static string? NormalizeRequired(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<Result> EnsureWarehousingEnabledAsync(Guid tenantId, CancellationToken ct) =>
        await featureService.EnsureEnabledAsync(
            tenantId,
            TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.WarehousingSubFeature,
            ct);

    public async Task<Result<Warehouse>> UpdateWarehouseAsync(UpdateWarehouseRequest request, CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<Warehouse>.Failure(tenantResult.Message!, tenantResult.StatusCode);
        var tenantId = tenantResult.Data;
        var featureResult = await EnsureWarehousingEnabledAsync(tenantId, ct);
        if (!featureResult.IsSuccess)
            return Result<Warehouse>.Failure(featureResult.Message!, featureResult.StatusCode);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
            return Result<Warehouse>.Failure("Name is required and must be at most 200 characters.", 400);

        if (request.Description?.Length > 1000) return Result<Warehouse>.Failure("Description must be at most 1000 characters.", 400);
        if (request.AddressLine?.Length > 500) return Result<Warehouse>.Failure("AddressLine must be at most 500 characters.", 400);
        if (request.City?.Length > 100) return Result<Warehouse>.Failure("City must be at most 100 characters.", 400);
        if (request.Region?.Length > 100) return Result<Warehouse>.Failure("Region must be at most 100 characters.", 400);
        if (request.PostalCode?.Length > 25) return Result<Warehouse>.Failure("PostalCode must be at most 25 characters.", 400);
        if (request.CountryCode?.Length > 3) return Result<Warehouse>.Failure("CountryCode must be at most 3 characters.", 400);
        var entity = await dataContext.Query<Warehouse>()
            .Where(x => x.TenantId == tenantId && x.Id == request.Id && !x.IsDeleted).FirstOrDefaultAsync(ct);
        if (entity is null) return Result<Warehouse>.NotFound("Record not found.");
        if (entity.ConcurrencyStamp != request.ConcurrencyStamp)
            return Result<Warehouse>.Conflict("This record changed. Refresh before saving.");
        dataContext.Update(entity);
        entity.Name = NormalizeOptional(request.Name);
        entity.Description = NormalizeOptional(request.Description);
        entity.AddressLine = NormalizeOptional(request.AddressLine);
        entity.City = NormalizeOptional(request.City);
        entity.Region = NormalizeOptional(request.Region);
        entity.PostalCode = NormalizeOptional(request.PostalCode);
        entity.CountryCode = NormalizeOptional(request.CountryCode);
        entity.ModifiedAt = DateTime.UtcNow;
        entity.ConcurrencyStamp = Guid.NewGuid();
        var saved = await dataContext.SaveChangesAsync(ct);
        return saved.IsSuccess ? Result<Warehouse>.Success(entity) : Result<Warehouse>.Failure("Could not save changes.", saved.StatusCode);
    }

    public async Task<Result<InventoryLocation>> UpdateInventoryLocationAsync(UpdateInventoryLocationRequest request, CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<InventoryLocation>.Failure(tenantResult.Message!, tenantResult.StatusCode);
        var tenantId = tenantResult.Data;
        var featureResult = await EnsureWarehousingEnabledAsync(tenantId, ct);
        if (!featureResult.IsSuccess)
            return Result<InventoryLocation>.Failure(featureResult.Message!, featureResult.StatusCode);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
            return Result<InventoryLocation>.Failure("Name is required and must be at most 200 characters.", 400);
        if (!Enum.IsDefined(request.LocationType))
            return Result<InventoryLocation>.Failure("Choose a valid location type.", 400);
        if (!request.IsPickable && await dataContext.Query<StockBalance>().AnyAsync(x => x.TenantId == tenantId && x.LocationId == request.Id && x.ReservedQuantity > 0 && !x.IsDeleted, ct))
            return Result<InventoryLocation>.Conflict("Release active reservations before disabling picking.");

        if (request.Description?.Length > 1000) return Result<InventoryLocation>.Failure("Description must be at most 1000 characters.", 400);
        var entity = await dataContext.Query<InventoryLocation>()
            .Where(x => x.TenantId == tenantId && x.Id == request.Id && !x.IsDeleted).FirstOrDefaultAsync(ct);
        if (entity is null) return Result<InventoryLocation>.NotFound("Record not found.");
        if (entity.ConcurrencyStamp != request.ConcurrencyStamp)
            return Result<InventoryLocation>.Conflict("This record changed. Refresh before saving.");
        dataContext.Update(entity);
        entity.Name = NormalizeOptional(request.Name);
        entity.Description = NormalizeOptional(request.Description);
        entity.LocationType = request.LocationType;
        entity.IsPickable = request.IsPickable;
        entity.ModifiedAt = DateTime.UtcNow;
        entity.ConcurrencyStamp = Guid.NewGuid();
        var saved = await dataContext.SaveChangesAsync(ct);
        return saved.IsSuccess ? Result<InventoryLocation>.Success(entity) : Result<InventoryLocation>.Failure("Could not save changes.", saved.StatusCode);
    }
}
