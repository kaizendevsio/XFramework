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

using XFramework.Integration.Security;

public sealed class InventoryReportingService(
    IDataContext dataContext,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
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
            request.ProductVariationId,
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
        var rows = await BuildExpiryRows(
            tenantResult.Data,
            request.ProductId,
            request.ProductVariationId,
            expiresAfter: now,
            expiresOnOrBefore: cutoff,
            includeExpiredStatus: false,
            ct);

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
        var rows = await BuildExpiryRows(
            tenantResult.Data,
            request.ProductId,
            request.ProductVariationId,
            expiresAfter: null,
            expiresOnOrBefore: now,
            includeExpiredStatus: true,
            ct);

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
        var balancesQuery = dataContext.Query<StockBalance>()
            .Where(x => x.TenantId == tenantId);

        if (request.ProductId is { } productId)
            balancesQuery = balancesQuery.Where(x => x.ProductId == productId);
        if (request.ProductVariationId is { } productVariationId)
            balancesQuery = balancesQuery.Where(x => x.ProductVariationId == productVariationId);
        if (request.WarehouseId is { } warehouseId)
            balancesQuery = balancesQuery.Where(x => x.WarehouseId == warehouseId);
        if (request.LocationId is { } locationId)
            balancesQuery = balancesQuery.Where(x => x.LocationId == locationId);
        if (request.LotId is { } lotId)
            balancesQuery = balancesQuery.Where(x => x.LotId == lotId);

        var balances = await balancesQuery
            .OrderBy(x => x.ProductId)
            .ThenBy(x => x.WarehouseId)
            .ThenBy(x => x.LocationId)
            .Take(1000)
            .ToListAsync(ct);

        var lookups = await LoadLookups(tenantId, balances, ct);
        var rows = balances.Select(balance => new StockPositionReportRow(
                balance.Id,
                balance.ProductId,
                lookups.ProductNames.GetValueOrDefault(balance.ProductId, balance.ProductId.ToString()[..8]),
                balance.ProductVariationId,
                balance.ProductVariationId is { } variationId
                    ? lookups.VariationNames.GetValueOrDefault(variationId, variationId.ToString()[..8])
                    : null,
                balance.ProductVariationId is { } variationTypeId
                    ? lookups.VariationTypeNames.GetValueOrDefault(variationTypeId)
                    : null,
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
        var movementsQuery = dataContext.Query<InventoryMovement>()
            .Where(x => x.TenantId == tenantId);

        if (request.ProductId is { } productId)
            movementsQuery = movementsQuery.Where(x => x.ProductId == productId);
        if (request.ProductVariationId is { } productVariationId)
            movementsQuery = movementsQuery.Where(x => x.ProductVariationId == productVariationId);
        if (request.WarehouseId is { } warehouseId)
            movementsQuery = movementsQuery.Where(x => x.WarehouseId == warehouseId);
        if (request.LocationId is { } locationId)
            movementsQuery = movementsQuery.Where(x => x.LocationId == locationId);
        if (request.LotId is { } lotId)
            movementsQuery = movementsQuery.Where(x => x.LotId == lotId);
        if (!string.IsNullOrWhiteSpace(request.ReferenceType))
            movementsQuery = movementsQuery.Where(x => x.ReferenceType == request.ReferenceType);
        if (request.ReferenceId is { } referenceId)
            movementsQuery = movementsQuery.Where(x => x.ReferenceId == referenceId);
        if (request.From is { } from)
            movementsQuery = movementsQuery.Where(x => x.MovementDate >= from);
        if (request.To is { } to)
            movementsQuery = movementsQuery.Where(x => x.MovementDate <= to);

        var movements = await movementsQuery
            .OrderByDescending(x => x.MovementDate)
            .Take(1000)
            .ToListAsync(ct);

        var lookups = await LoadLookups(tenantId, movements, ct);
        var rows = movements
            .Select(movement => new MovementLedgerReportRow(
                movement.Id,
                movement.ProductId,
                lookups.ProductNames.GetValueOrDefault(movement.ProductId, movement.ProductId.ToString()[..8]),
                movement.ProductVariationId,
                movement.ProductVariationId is { } variationId
                    ? lookups.VariationNames.GetValueOrDefault(variationId, variationId.ToString()[..8])
                    : null,
                movement.ProductVariationId is { } variationTypeId
                    ? lookups.VariationTypeNames.GetValueOrDefault(variationTypeId)
                    : null,
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
        var allocationsQuery = dataContext.Query<ReservationAllocation>()
            .Where(x => x.TenantId == tenantId);

        if (request.ProductId is { } productId)
            allocationsQuery = allocationsQuery.Where(x => x.ProductId == productId);
        if (request.ProductVariationId is { } productVariationId)
            allocationsQuery = allocationsQuery.Where(x => x.ProductVariationId == productVariationId);
        if (request.LotId is { } lotId)
            allocationsQuery = allocationsQuery.Where(x => x.LotId == lotId);
        if (request.Status is { } status)
            allocationsQuery = allocationsQuery.Where(x => x.Status == status);

        var allocations = await allocationsQuery
            .OrderByDescending(x => x.ReservedAt)
            .Take(1000)
            .ToListAsync(ct);

        var lookups = await LoadLookups(tenantId, allocations, ct);
        var rows = allocations
            .Select(allocation => new ReservationAllocationStatusReportRow(
                allocation.Id,
                allocation.ReservationId,
                allocation.ProductId,
                lookups.ProductNames.GetValueOrDefault(allocation.ProductId, allocation.ProductId.ToString()[..8]),
                allocation.ProductVariationId,
                allocation.ProductVariationId is { } variationId
                    ? lookups.VariationNames.GetValueOrDefault(variationId, variationId.ToString()[..8])
                    : null,
                allocation.ProductVariationId is { } variationTypeId
                    ? lookups.VariationTypeNames.GetValueOrDefault(variationTypeId)
                    : null,
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
        Guid? productVariationId,
        DateTime? expiresAfter,
        DateTime expiresOnOrBefore,
        bool includeExpiredStatus,
        CancellationToken ct)
    {
        var lotsQuery = dataContext.Query<InventoryLot>()
            .Where(x => x.TenantId == tenantId);
        if (productId is { } id)
            lotsQuery = lotsQuery.Where(x => x.ProductId == id);
        if (productVariationId is { } variantId)
            lotsQuery = lotsQuery.Where(x => x.ProductVariationId == variantId);

        lotsQuery = includeExpiredStatus
            ? lotsQuery.Where(x => x.Status == InventoryLotStatus.Expired || x.ExpiresAt != null && x.ExpiresAt <= expiresOnOrBefore)
            : lotsQuery.Where(x => x.ExpiresAt != null && x.ExpiresAt > expiresAfter && x.ExpiresAt <= expiresOnOrBefore);

        var lots = await lotsQuery
            .OrderBy(x => x.ExpiresAt)
            .Take(1000)
            .ToListAsync(ct);
        if (lots.Count == 0)
            return [];

        var lotIds = lots.Select(x => x.Id).ToList();
        var balances = await dataContext.Query<StockBalance>()
            .Where(x =>
                x.TenantId == tenantId &&
                x.LotId != null &&
                lotIds.Contains(x.LotId.Value) &&
                x.OnHandQuantity > 0)
            .Take(1000)
            .ToListAsync(ct);

        var lotMap = lots.ToDictionary(x => x.Id);
        var lookups = await LoadLookups(tenantId, balances, ct);

        return balances.Select(balance =>
            {
                var lot = lotMap[balance.LotId!.Value];
                return new NearExpiryStockReportRow(
                    lot.Id,
                    lot.LotNumber ?? lot.Id.ToString()[..8],
                    lot.ProductId,
                    lookups.ProductNames.GetValueOrDefault(lot.ProductId, lot.ProductId.ToString()[..8]),
                    lot.ProductVariationId,
                    lot.ProductVariationId is { } variationId
                        ? lookups.VariationNames.GetValueOrDefault(variationId, variationId.ToString()[..8])
                        : null,
                    lot.ProductVariationId is { } variationTypeId
                        ? lookups.VariationTypeNames.GetValueOrDefault(variationTypeId)
                        : null,
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

    private Task<LookupMaps> LoadLookups(Guid tenantId, IReadOnlyCollection<StockBalance> rows, CancellationToken ct) =>
        LoadLookups(
            tenantId,
            rows.Select(x => x.ProductId),
            rows.Select(x => x.ProductVariationId),
            rows.Select(x => (Guid?)x.WarehouseId),
            rows.Select(x => (Guid?)x.LocationId),
            rows.Select(x => x.LotId),
            ct);

    private Task<LookupMaps> LoadLookups(Guid tenantId, IReadOnlyCollection<InventoryMovement> rows, CancellationToken ct) =>
        LoadLookups(
            tenantId,
            rows.Select(x => x.ProductId),
            rows.Select(x => x.ProductVariationId),
            rows.Select(x => x.WarehouseId),
            rows.Select(x => x.LocationId),
            rows.Select(x => x.LotId),
            ct);

    private Task<LookupMaps> LoadLookups(Guid tenantId, IReadOnlyCollection<ReservationAllocation> rows, CancellationToken ct) =>
        LoadLookups(
            tenantId,
            rows.Select(x => x.ProductId),
            rows.Select(x => x.ProductVariationId),
            [],
            [],
            rows.Select(x => x.LotId),
            ct);

    private async Task<LookupMaps> LoadLookups(
        Guid tenantId,
        IEnumerable<Guid> productIds,
        IEnumerable<Guid?> variationIds,
        IEnumerable<Guid?> warehouseIds,
        IEnumerable<Guid?> locationIds,
        IEnumerable<Guid?> lotIds,
        CancellationToken ct)
    {
        var productIdList = productIds.Distinct().ToList();
        var variationIdList = variationIds.OfType<Guid>().Distinct().ToList();
        var warehouseIdList = warehouseIds.OfType<Guid>().Distinct().ToList();
        var locationIdList = locationIds.OfType<Guid>().Distinct().ToList();
        var lotIdList = lotIds.OfType<Guid>().Distinct().ToList();

        var products = await dataContext.Query<Product>()
            .Where(x => x.TenantId == tenantId && productIdList.Contains(x.Id))
            .ToListAsync(ct);
        var warehouses = await dataContext.Query<Warehouse>()
            .Where(x => x.TenantId == tenantId && warehouseIdList.Contains(x.Id))
            .ToListAsync(ct);
        var locations = await dataContext.Query<InventoryLocation>()
            .Where(x => x.TenantId == tenantId && locationIdList.Contains(x.Id))
            .ToListAsync(ct);
        var lots = await dataContext.Query<InventoryLot>()
            .Where(x => x.TenantId == tenantId && lotIdList.Contains(x.Id))
            .ToListAsync(ct);
        var variations = await dataContext.Query<ProductVariation>()
            .Where(x => x.TenantId == tenantId && variationIdList.Contains(x.Id))
            .ToListAsync(ct);
        var typeIds = variations
            .Where(x => x.ProductVariationTypeId is not null)
            .Select(x => x.ProductVariationTypeId!.Value)
            .Distinct()
            .ToList();
        var variationTypes = typeIds.Count == 0
            ? []
            : await dataContext.Query<ProductVariationType>()
                .Where(x => x.TenantId == tenantId && typeIds.Contains(x.Id))
                .ToListAsync(ct);
        var variationTypeNames = variationTypes.ToDictionary<ProductVariationType, Guid, string?>(
            x => x.Id,
            x => x.Name ?? x.Id.ToString()[..8]);

        return new LookupMaps(
            products.ToDictionary(x => x.Id, x => x.Name ?? x.Id.ToString()[..8]),
            warehouses.ToDictionary(x => x.Id, x => $"{x.Code} - {x.Name}"),
            locations.ToDictionary(x => x.Id, x => $"{x.Code} - {x.Name}"),
            lots.ToDictionary(x => x.Id, x => x.LotNumber ?? x.Id.ToString()[..8]),
            variations.ToDictionary(x => x.Id, x => x.Name ?? x.Id.ToString()[..8]),
            variations.ToDictionary(
                x => x.Id,
                x => x.ProductVariationTypeId is { } typeId
                    ? variationTypeNames.GetValueOrDefault(typeId, x.VariationType)
                    : x.VariationType));
    }

    private Result<Guid> GetCurrentTenantId(RequestBase? request)
    {
        var tenantId = trustedInvocationContextAccessor.Current?.EffectiveTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result<Guid>.Unauthorized("Authentication is required for inventory reporting operations.");
        return Result<Guid>.Success(tenantId.Value);
    }

    private sealed record LookupMaps(
        Dictionary<Guid, string> ProductNames,
        Dictionary<Guid, string> WarehouseNames,
        Dictionary<Guid, string> LocationNames,
        Dictionary<Guid, string> LotNumbers,
        Dictionary<Guid, string> VariationNames,
        Dictionary<Guid, string?> VariationTypeNames);

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
