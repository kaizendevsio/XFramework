using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Api.Services;

public sealed class InventoryReportingService(
    IDataContext dataContext,
    IHttpContextAccessor httpContextAccessor,
    InventoryPlanningService planningService,
    ITenantModuleFeatureService featureService)
{
    public async Task<Result<List<LowStockReportRow>>> GetLowStockAsync(
        GetLowStockReportRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<LowStockReportRow>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureReportingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<LowStockReportRow>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var rows = await planningService.BuildLowStockRowsAsync(
            tenantResult.Data,
            request.ProductId,
            request.WarehouseId,
            request.LocationId,
            ct);

        return Result<List<LowStockReportRow>>.Success(rows);
    }

    public async Task<Result<List<NearExpiryStockReportRow>>> GetNearExpiryAsync(
        GetNearExpiryStockReportRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<NearExpiryStockReportRow>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureReportingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<NearExpiryStockReportRow>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var traceabilityResult = await EnsureTraceabilityEnabledAsync(tenantResult.Data, ct);
        if (!traceabilityResult.IsSuccess)
            return Result<List<NearExpiryStockReportRow>>.Failure(traceabilityResult.Message!, traceabilityResult.StatusCode);

        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(Math.Clamp(request.DaysAhead, 1, 365));
        var rows = await BuildExpiryRows(tenantResult.Data, request.ProductId, lot =>
            lot.ExpiresAt is not null && lot.ExpiresAt > now && lot.ExpiresAt <= cutoff, ct);

        return Result<List<NearExpiryStockReportRow>>.Success(rows);
    }

    public async Task<Result<List<NearExpiryStockReportRow>>> GetExpiredAsync(
        GetExpiredStockReportRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<NearExpiryStockReportRow>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureReportingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<NearExpiryStockReportRow>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var traceabilityResult = await EnsureTraceabilityEnabledAsync(tenantResult.Data, ct);
        if (!traceabilityResult.IsSuccess)
            return Result<List<NearExpiryStockReportRow>>.Failure(traceabilityResult.Message!, traceabilityResult.StatusCode);

        var now = DateTime.UtcNow;
        var rows = await BuildExpiryRows(tenantResult.Data, request.ProductId, lot =>
            lot.Status == InventoryLotStatus.Expired || lot.ExpiresAt is not null && lot.ExpiresAt <= now, ct);

        return Result<List<NearExpiryStockReportRow>>.Success(rows);
    }

    public async Task<Result<List<StockPositionReportRow>>> GetStockPositionsAsync(
        GetStockPositionReportRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<StockPositionReportRow>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureReportingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<StockPositionReportRow>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var tenantId = tenantResult.Data;
        var balances = await dataContext.Query<StockBalance>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);

        if (request.ProductId is { } productId)
            balances = balances.Where(x => x.ProductId == productId).ToList();
        if (request.WarehouseId is { } warehouseId)
            balances = balances.Where(x => x.WarehouseId == warehouseId).ToList();
        if (request.LocationId is { } locationId)
            balances = balances.Where(x => x.LocationId == locationId).ToList();
        if (request.LotId is { } lotId)
            balances = balances.Where(x => x.LotId == lotId).ToList();

        var lookups = await LoadLookups(tenantId, ct);
        var rows = balances.Select(balance => new StockPositionReportRow(
                balance.Id,
                balance.ProductId,
                lookups.ProductNames.GetValueOrDefault(balance.ProductId, balance.ProductId.ToString()[..8]),
                balance.WarehouseId,
                lookups.WarehouseNames.GetValueOrDefault(balance.WarehouseId, balance.WarehouseId.ToString()[..8]),
                balance.LocationId,
                lookups.LocationNames.GetValueOrDefault(balance.LocationId, balance.LocationId.ToString()[..8]),
                balance.LotId,
                balance.LotId is { } lotId ? lookups.LotNumbers.GetValueOrDefault(lotId, lotId.ToString()[..8]) : null,
                balance.OnHandQuantity,
                balance.ReservedQuantity,
                balance.AvailableQuantity))
            .OrderBy(x => x.ProductName)
            .ThenBy(x => x.WarehouseName)
            .ThenBy(x => x.LocationName)
            .ToList();

        return Result<List<StockPositionReportRow>>.Success(rows);
    }

    public async Task<Result<List<MovementLedgerReportRow>>> GetMovementLedgerAsync(
        GetMovementLedgerReportRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<MovementLedgerReportRow>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureReportingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<MovementLedgerReportRow>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var tenantId = tenantResult.Data;
        var movements = await dataContext.Query<InventoryMovement>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);

        if (request.ProductId is { } productId)
            movements = movements.Where(x => x.ProductId == productId).ToList();
        if (request.WarehouseId is { } warehouseId)
            movements = movements.Where(x => x.WarehouseId == warehouseId).ToList();
        if (request.LocationId is { } locationId)
            movements = movements.Where(x => x.LocationId == locationId).ToList();
        if (request.LotId is { } lotId)
            movements = movements.Where(x => x.LotId == lotId).ToList();
        if (!string.IsNullOrWhiteSpace(request.ReferenceType))
            movements = movements.Where(x => x.ReferenceType == request.ReferenceType).ToList();
        if (request.ReferenceId is { } referenceId)
            movements = movements.Where(x => x.ReferenceId == referenceId).ToList();
        if (request.From is { } from)
            movements = movements.Where(x => x.MovementDate >= from).ToList();
        if (request.To is { } to)
            movements = movements.Where(x => x.MovementDate <= to).ToList();

        var lookups = await LoadLookups(tenantId, ct);
        var rows = movements
            .OrderByDescending(x => x.MovementDate)
            .Take(1000)
            .Select(movement => new MovementLedgerReportRow(
                movement.Id,
                movement.ProductId,
                lookups.ProductNames.GetValueOrDefault(movement.ProductId, movement.ProductId.ToString()[..8]),
                movement.WarehouseId,
                movement.WarehouseId is { } warehouseId
                    ? lookups.WarehouseNames.GetValueOrDefault(warehouseId, warehouseId.ToString()[..8])
                    : "N/A",
                movement.LocationId,
                movement.LocationId is { } locationId
                    ? lookups.LocationNames.GetValueOrDefault(locationId, locationId.ToString()[..8])
                    : "N/A",
                movement.LotId,
                movement.LotId is { } lotId ? lookups.LotNumbers.GetValueOrDefault(lotId, lotId.ToString()[..8]) : null,
                movement.MovementType,
                movement.QuantityDelta,
                movement.ReferenceType,
                movement.ReferenceId,
                movement.MovementDate))
            .ToList();

        return Result<List<MovementLedgerReportRow>>.Success(rows);
    }

    public async Task<Result<List<ReservationAllocationStatusReportRow>>> GetAllocationStatusAsync(
        GetReservationAllocationStatusReportRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<ReservationAllocationStatusReportRow>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureReportingEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<ReservationAllocationStatusReportRow>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var tenantId = tenantResult.Data;
        var allocations = await dataContext.Query<ReservationAllocation>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);

        if (request.ProductId is { } productId)
            allocations = allocations.Where(x => x.ProductId == productId).ToList();
        if (request.LotId is { } lotId)
            allocations = allocations.Where(x => x.LotId == lotId).ToList();
        if (request.Status is { } status)
            allocations = allocations.Where(x => x.Status == status).ToList();

        var lookups = await LoadLookups(tenantId, ct);
        var rows = allocations
            .OrderByDescending(x => x.ReservedAt)
            .Take(1000)
            .Select(allocation => new ReservationAllocationStatusReportRow(
                allocation.Id,
                allocation.ReservationId,
                allocation.ProductId,
                lookups.ProductNames.GetValueOrDefault(allocation.ProductId, allocation.ProductId.ToString()[..8]),
                allocation.LotId,
                allocation.LotId is { } lotId ? lookups.LotNumbers.GetValueOrDefault(lotId, lotId.ToString()[..8]) : null,
                allocation.Quantity,
                allocation.Status,
                allocation.ReservedAt,
                allocation.ReleasedAt,
                allocation.FulfilledAt))
            .ToList();

        return Result<List<ReservationAllocationStatusReportRow>>.Success(rows);
    }

    private async Task<List<NearExpiryStockReportRow>> BuildExpiryRows(
        Guid tenantId,
        Guid? productId,
        Func<InventoryLot, bool> lotFilter,
        CancellationToken ct)
    {
        var lots = await dataContext.Query<InventoryLot>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);
        if (productId is { } id)
            lots = lots.Where(x => x.ProductId == id).ToList();

        lots = lots.Where(lotFilter).ToList();
        if (lots.Count == 0)
            return [];

        var lotIds = lots.Select(x => x.Id).ToHashSet();
        var balances = await dataContext.Query<StockBalance>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.LotId != null && !x.IsDeleted)
            .ToListAsync(ct);
        balances = balances.Where(x => x.LotId is { } lotId && lotIds.Contains(lotId) && x.OnHandQuantity > 0).ToList();

        var lotMap = lots.ToDictionary(x => x.Id);
        var lookups = await LoadLookups(tenantId, ct);

        return balances.Select(balance =>
            {
                var lot = lotMap[balance.LotId!.Value];
                return new NearExpiryStockReportRow(
                    lot.Id,
                    lot.LotNumber ?? lot.Id.ToString()[..8],
                    lot.ProductId,
                    lookups.ProductNames.GetValueOrDefault(lot.ProductId, lot.ProductId.ToString()[..8]),
                    balance.WarehouseId,
                    lookups.WarehouseNames.GetValueOrDefault(balance.WarehouseId, balance.WarehouseId.ToString()[..8]),
                    balance.LocationId,
                    lookups.LocationNames.GetValueOrDefault(balance.LocationId, balance.LocationId.ToString()[..8]),
                    balance.OnHandQuantity,
                    balance.AvailableQuantity,
                    lot.ExpiresAt,
                    lot.Status);
            })
            .OrderBy(x => x.ExpiresAt)
            .ThenBy(x => x.ProductName)
            .ToList();
    }

    private async Task<LookupMaps> LoadLookups(Guid tenantId, CancellationToken ct)
    {
        var products = await dataContext.Query<Product>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);
        var warehouses = await dataContext.Query<Warehouse>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);
        var locations = await dataContext.Query<InventoryLocation>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);
        var lots = await dataContext.Query<InventoryLot>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);

        return new LookupMaps(
            products.ToDictionary(x => x.Id, x => x.Name ?? x.Id.ToString()[..8]),
            warehouses.ToDictionary(x => x.Id, x => $"{x.Code} - {x.Name}"),
            locations.ToDictionary(x => x.Id, x => $"{x.Code} - {x.Name}"),
            lots.ToDictionary(x => x.Id, x => x.LotNumber ?? x.Id.ToString()[..8]));
    }

    private Result<Guid> GetCurrentTenantId(RequestBase? request)
    {
        if (request?.Metadata?.TenantId is { } metadataTenantId && metadataTenantId != Guid.Empty)
            return Result<Guid>.Success(metadataTenantId);

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return Result<Guid>.Unauthorized("Authentication is required for inventory reporting operations.");

        var tenantIdClaim = user.FindFirst("tenantId")?.Value
            ?? user.FindFirst("TenantId")?.Value
            ?? user.FindFirst("tid")?.Value;

        if (Guid.TryParse(tenantIdClaim, out var tenantId) && tenantId != Guid.Empty)
            return Result<Guid>.Success(tenantId);

        return Result<Guid>.Forbidden("Authenticated user does not have a valid tenant context.");
    }

    private sealed record LookupMaps(
        Dictionary<Guid, string> ProductNames,
        Dictionary<Guid, string> WarehouseNames,
        Dictionary<Guid, string> LocationNames,
        Dictionary<Guid, string> LotNumbers);

    private async Task<Result> EnsureReportingEnabledAsync(Guid tenantId, CancellationToken ct) =>
        await featureService.EnsureEnabledAsync(
            tenantId,
            TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.ReportingSubFeature,
            ct);

    private async Task<Result> EnsureTraceabilityEnabledAsync(Guid tenantId, CancellationToken ct) =>
        await featureService.EnsureEnabledAsync(
            tenantId,
            TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.TraceabilitySubFeature,
            ct);
}
