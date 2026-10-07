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
            .NoCache()
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
            .ThenBy(x => x.Id)
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
            .NoCache()
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
            .ThenBy(x => x.Id)
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

    public async Task<Result<InventoryReportSnapshot>> GetSnapshotAsync(
        GetInventoryReportSnapshotRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var tenant = GetCurrentTenantId(request);
        if (!tenant.IsSuccess)
            return Result<InventoryReportSnapshot>.Failure(tenant.Message!, tenant.StatusCode);
        if (request.Metadata?.RequestedTenantId != tenant.Data)
            return Result<InventoryReportSnapshot>.Forbidden("An explicitly authorized report tenant is required.");
        if (request.FromUtc.Kind != DateTimeKind.Utc || request.ToUtc.Kind != DateTimeKind.Utc ||
            request.FromUtc > request.ToUtc || request.ToUtc - request.FromUtc > TimeSpan.FromDays(366) ||
            request.DaysAhead is < 1 or > 365)
            return Result<InventoryReportSnapshot>.Failure("Choose a valid UTC date range of at most 366 days and expiry window of 1-365 days.", 400);
        var enabled = await EnsureReportingEnabledAsync(tenant.Data, ct);
        if (!enabled.IsSuccess)
            return Result<InventoryReportSnapshot>.Failure(enabled.Message!, enabled.StatusCode);

        var asOf = DateTime.UtcNow;
        var to = request.ToUtc < asOf ? request.ToUtc : asOf;
        IRemoteQuery<StockBalance> Balances() => dataContext.Query<StockBalance>().NoCache().Where(x =>
            x.TenantId == tenant.Data &&
            (request.ProductId == null || x.ProductId == request.ProductId) &&
            (request.WarehouseId == null || x.WarehouseId == request.WarehouseId) &&
            (request.LocationId == null || x.LocationId == request.LocationId));
        IRemoteQuery<InventoryMovement> Movements() => dataContext.Query<InventoryMovement>().NoCache().Where(x =>
            x.TenantId == tenant.Data && x.MovementDate >= request.FromUtc && x.MovementDate <= to &&
            (request.ProductId == null || x.ProductId == request.ProductId) &&
            (request.WarehouseId == null || x.WarehouseId == request.WarehouseId) &&
            (request.LocationId == null || x.LocationId == request.LocationId));

        // Scalar aggregates cover the full scope; only detail rows are capped.
        var positionCount = await Balances().CountAsync(ct);
        var onHand = await Balances().SumAsync(x => x.OnHandQuantity, ct);
        var reserved = await Balances().SumAsync(x => x.ReservedQuantity, ct);
        var available = await Balances().SumAsync(x => x.AvailableQuantity, ct);
        var movementCount = await Movements().CountAsync(ct);
        var inbound = await Movements().Where(x => x.MovementType != InventoryMovementType.Reservation &&
            x.MovementType != InventoryMovementType.Release && x.QuantityDelta > 0).SumAsync(x => x.QuantityDelta, ct);
        var outbound = -await Movements().Where(x => x.MovementType != InventoryMovementType.Reservation &&
            x.MovementType != InventoryMovementType.Release && x.QuantityDelta < 0).SumAsync(x => x.QuantityDelta, ct);
        var positions = await GetStockPositionsAsync(new()
        {
            Metadata = request.Metadata, ProductId = request.ProductId,
            WarehouseId = request.WarehouseId, LocationId = request.LocationId
        }, ct);
        var movements = await GetMovementLedgerAsync(new()
        {
            Metadata = request.Metadata, ProductId = request.ProductId,
            WarehouseId = request.WarehouseId, LocationId = request.LocationId,
            From = request.FromUtc, To = to
        }, ct);
        if (!positions.IsSuccess || !movements.IsSuccess)
            return Result<InventoryReportSnapshot>.Failure("Report access changed. Refresh the report.", 403);
        var traceability = await featureService.IsEnabledAsync(tenant.Data,
            TenantModuleFeatureKeys.Inventario, TenantModuleFeatureKeys.TraceabilitySubFeature, ct);
        if (!traceability.IsSuccess)
            return Result<InventoryReportSnapshot>.Failure("Could not verify expiry report access.", traceability.StatusCode);
        var near = traceability.Data
            ? await BuildExpiryRows(tenant.Data, request.ProductId, null, asOf,
                asOf.AddDays(request.DaysAhead), false, ct, request.WarehouseId, request.LocationId)
            : [];
        var expired = traceability.Data
            ? await BuildExpiryRows(tenant.Data, request.ProductId, null, null,
                asOf, true, ct, request.WarehouseId, request.LocationId)
            : [];
        var products = await dataContext.Query<Product>().NoCache().Where(x => x.TenantId == tenant.Data)
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Take(500).ToListAsync(ct);
        var warehouses = await dataContext.Query<Warehouse>().NoCache().Where(x => x.TenantId == tenant.Data)
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Take(500).ToListAsync(ct);
        var locations = await dataContext.Query<InventoryLocation>().NoCache().Where(x => x.TenantId == tenant.Data)
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Take(500).ToListAsync(ct);
        return Result<InventoryReportSnapshot>.Success(new(tenant.Data, asOf, onHand, reserved, available,
            inbound, outbound, positionCount, movementCount, traceability.Data,
            positions.Data!, movements.Data!, near, expired,
            products.Select(x => new InventoryReportFilterOption(x.Id, x.Name ?? "Unnamed product")).ToList(),
            warehouses.Select(x => new InventoryReportFilterOption(x.Id, $"{x.Code} - {x.Name}")).ToList(),
            locations.Select(x => new InventoryReportFilterOption(x.Id, $"{x.Code} - {x.Name}", x.WarehouseId)).ToList()));
    }

    private async Task<List<NearExpiryStockReportRow>> BuildExpiryRows(
        Guid tenantId,
        Guid? productId,
        Guid? productVariationId,
        DateTime? expiresAfter,
        DateTime expiresOnOrBefore,
        bool includeExpiredStatus,
        CancellationToken ct,
        Guid? warehouseId = null,
        Guid? locationId = null)
    {
        var balancesQuery = dataContext.Query<StockBalance>().NoCache()
            .Where(x => x.TenantId == tenantId && x.OnHandQuantity > 0 &&
                x.Lot != null && x.Lot.TenantId == tenantId);
        if (productId is { } id)
            balancesQuery = balancesQuery.Where(x => x.ProductId == id);
        if (productVariationId is { } variantId)
            balancesQuery = balancesQuery.Where(x => x.ProductVariationId == variantId);
        if (warehouseId is { } warehouse)
            balancesQuery = balancesQuery.Where(x => x.WarehouseId == warehouse);
        if (locationId is { } location)
            balancesQuery = balancesQuery.Where(x => x.LocationId == location);
        // Apply storage and expiry scope in SQL before the cap, not to a capped list of tenant lots.
        balancesQuery = includeExpiredStatus
            ? balancesQuery.Where(x => x.Lot!.Status == InventoryLotStatus.Expired ||
                x.Lot.ExpiresAt != null && x.Lot.ExpiresAt <= expiresOnOrBefore)
            : balancesQuery.Where(x => x.Lot!.ExpiresAt != null &&
                x.Lot.ExpiresAt > expiresAfter && x.Lot.ExpiresAt <= expiresOnOrBefore);
        var balances = await balancesQuery.OrderBy(x => x.Lot!.ExpiresAt).ThenBy(x => x.Id).Take(1000)
            .ToListAsync(ct);
        var lotIds = balances.Select(x => x.LotId!.Value).Distinct().ToList();
        var lots = await dataContext.Query<InventoryLot>().NoCache()
            .Where(x => x.TenantId == tenantId && lotIds.Contains(x.Id)).Take(1000).ToListAsync(ct);
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
        var context = trustedInvocationContextAccessor.Current;
        var tenantId = context?.EffectiveTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result<Guid>.Unauthorized("Authentication is required for inventory reporting operations.");
        if (context?.Actor is not { } actor || actor.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return Result<Guid>.Unauthorized("Sign in again to view inventory reports.");
        if (!actor.Capabilities.Contains("inventario.reporting:view"))
            return Result<Guid>.Forbidden("Inventory reporting view permission is required.");
        if (request?.Metadata?.RequestedTenantId is { } target && target != tenantId)
            return Result<Guid>.Forbidden("The report tenant does not match the authorized tenant.");
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
