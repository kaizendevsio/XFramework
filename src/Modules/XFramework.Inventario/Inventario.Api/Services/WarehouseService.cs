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

        var warehouses = await dataContext.Query<Warehouse>()
            .Where(x => x.TenantId == tenantResult.Data && !x.IsDeleted)
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
}
