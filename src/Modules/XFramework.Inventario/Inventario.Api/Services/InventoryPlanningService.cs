using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reports;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace XFramework.Inventario.Api.Services;

using XFramework.Integration.Security;

public sealed class InventoryPlanningService(
    IDataContext dataContext,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
    ITenantModuleFeatureService featureService,
    ProductVariationService productVariationService)
{
    public async Task<Result<List<InventoryReorderRule>>> GetRulesAsync(
        GetInventoryReorderRulesRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<InventoryReorderRule>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePlanningEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<InventoryReorderRule>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var tenantId = tenantResult.Data;
        var query = dataContext.Query<InventoryReorderRule>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (request.ProductId is { } productId)
            query = query.Where(x => x.ProductId == productId);

        if (request.ProductVariationId is { } productVariationId)
            query = query.Where(x => x.ProductVariationId == productVariationId);

        if (!request.IncludeInactive)
            query = query.Where(x => x.IsActive);

        var rules = await query
            .OrderBy(x => x.ProductId)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<InventoryReorderRule>>.Success(rules);
    }

    public async Task<Result<InventoryReorderRule>> CreateRuleAsync(
        CreateInventoryReorderRuleRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<InventoryReorderRule>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePlanningEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<InventoryReorderRule>.Failure(featureResult.Message!, featureResult.StatusCode);

        if (request.MinimumQuantity < 0 || request.ReorderPoint < 0 || request.ReorderQuantity <= 0)
            return Result<InventoryReorderRule>.Failure("Reorder quantities must be valid positive values.", 400);

        if (request.MaximumQuantity is { } maximum && maximum < request.MinimumQuantity)
            return Result<InventoryReorderRule>.Failure("Maximum quantity must be greater than or equal to minimum quantity.", 400);

        var tenantId = tenantResult.Data;
        var productExists = await dataContext.Query<Product>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == request.ProductId && !x.IsDeleted, ct);
        if (!productExists)
            return Result<InventoryReorderRule>.NotFound("Product not found.");

        var variationResult = await productVariationService.ValidateProductVariationAsync(
            tenantId,
            request.ProductId,
            request.ProductVariationId,
            ct);
        if (!variationResult.IsSuccess)
            return Result<InventoryReorderRule>.Failure(variationResult.Message!, variationResult.StatusCode);

        if (request.WarehouseId is { } warehouseId)
        {
            var warehouseExists = await dataContext.Query<Warehouse>()
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.Id == warehouseId && !x.IsDeleted, ct);
            if (!warehouseExists)
                return Result<InventoryReorderRule>.NotFound("Warehouse not found.");
        }

        if (request.LocationId is { } locationId)
        {
            var locationExists = await dataContext.Query<InventoryLocation>()
                .IgnoreQueryFilters()
                .AnyAsync(x =>
                    x.TenantId == tenantId &&
                    x.Id == locationId &&
                    (request.WarehouseId == null || x.WarehouseId == request.WarehouseId) &&
                    !x.IsDeleted, ct);
            if (!locationExists)
                return Result<InventoryReorderRule>.NotFound("Location not found.");
        }

        var duplicate = await dataContext.Query<InventoryReorderRule>()
            .IgnoreQueryFilters()
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ProductId == request.ProductId &&
                x.ProductVariationId == request.ProductVariationId &&
                x.WarehouseId == request.WarehouseId &&
                x.LocationId == request.LocationId &&
                !x.IsDeleted, ct);
        if (duplicate)
            return Result<InventoryReorderRule>.Conflict("A reorder rule already exists for this product scope.");

        var now = DateTime.UtcNow;
        var rule = new InventoryReorderRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = request.ProductId,
            ProductVariationId = request.ProductVariationId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            MinimumQuantity = request.MinimumQuantity,
            MaximumQuantity = request.MaximumQuantity,
            ReorderPoint = request.ReorderPoint,
            ReorderQuantity = request.ReorderQuantity,
            PreferredSupplier = NormalizeOptional(request.PreferredSupplier),
            IsActive = request.IsActive,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(rule);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<InventoryReorderRule>.Failure(saveResult.Message ?? "Reorder rule save failed.", saveResult.StatusCode);

        return Result<InventoryReorderRule>.Success(rule, 201, "Reorder rule created.");
    }

    public async Task<Result<List<LowStockReportRow>>> GetLowStockAsync(
        GetLowStockReportRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<LowStockReportRow>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePlanningEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<LowStockReportRow>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var rows = await BuildLowStockRowsAsync(
            tenantResult.Data,
            request.ProductId,
            request.ProductVariationId,
            request.WarehouseId,
            request.LocationId,
            ct);
        return Result<List<LowStockReportRow>>.Success(rows);
    }

    public async Task<Result<List<ReorderSuggestionRow>>> GetReorderSuggestionsAsync(
        GetReorderSuggestionsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<ReorderSuggestionRow>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePlanningEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<ReorderSuggestionRow>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var lowStock = await BuildLowStockRowsAsync(
            tenantResult.Data,
            request.ProductId,
            request.ProductVariationId,
            request.WarehouseId,
            request.LocationId,
            ct);
        var rules = await LoadRules(
            tenantResult.Data,
            request.ProductId,
            request.ProductVariationId,
            request.WarehouseId,
            request.LocationId,
            ct);

        var suggestions = lowStock.Select(row =>
        {
            var rule = rules.First(x =>
                x.ProductId == row.ProductId &&
                x.ProductVariationId == row.ProductVariationId &&
                x.WarehouseId == row.WarehouseId &&
                x.LocationId == row.LocationId);
            var targetQuantity = rule.MaximumQuantity ?? row.ReorderPoint + rule.ReorderQuantity;
            var neededToTarget = Math.Max(0, targetQuantity - row.AvailableQuantity);
            var suggested = Math.Max(rule.ReorderQuantity, neededToTarget);
            return new ReorderSuggestionRow(
                row.ProductId,
                row.ProductName,
                row.ProductVariationId,
                row.ProductVariationName,
                row.ProductVariationTypeName,
                row.WarehouseId,
                row.WarehouseName,
                row.LocationId,
                row.LocationName,
                row.AvailableQuantity,
                row.ReorderPoint,
                suggested,
                rule.PreferredSupplier);
        }).ToList();

        return Result<List<ReorderSuggestionRow>>.Success(suggestions);
    }

    internal async Task<List<LowStockReportRow>> BuildLowStockRowsAsync(
        Guid tenantId,
        Guid? productId,
        Guid? productVariationId,
        Guid? warehouseId,
        Guid? locationId,
        CancellationToken ct)
    {
        var rules = await LoadRules(tenantId, productId, productVariationId, warehouseId, locationId, ct);
        if (rules.Count == 0)
            return [];

        var products = await dataContext.Query<Product>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);
        var variationLookups = await LoadVariationLookups(tenantId, ct);
        var warehouses = await dataContext.Query<Warehouse>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);
        var locations = await dataContext.Query<InventoryLocation>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);
        var balances = await dataContext.Query<StockBalance>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);

        var productNames = products.ToDictionary(x => x.Id, x => x.Name ?? x.Id.ToString()[..8]);
        var warehouseNames = warehouses.ToDictionary(x => x.Id, x => $"{x.Code} - {x.Name}");
        var locationNames = locations.ToDictionary(x => x.Id, x => $"{x.Code} - {x.Name}");
        var rows = new List<LowStockReportRow>();

        foreach (var rule in rules)
        {
            var available = balances
                .Where(x =>
                    x.ProductId == rule.ProductId &&
                    (rule.ProductVariationId is null || x.ProductVariationId == rule.ProductVariationId) &&
                    (rule.WarehouseId is null || x.WarehouseId == rule.WarehouseId) &&
                    (rule.LocationId is null || x.LocationId == rule.LocationId))
                .Sum(x => x.AvailableQuantity);
            var reorderPoint = rule.ReorderPoint > 0 ? rule.ReorderPoint : rule.MinimumQuantity;
            if (available > reorderPoint)
                continue;

            rows.Add(new LowStockReportRow(
                rule.ProductId,
                productNames.GetValueOrDefault(rule.ProductId, rule.ProductId.ToString()[..8]),
                rule.ProductVariationId,
                rule.ProductVariationId is { } variationId
                    ? variationLookups.Names.GetValueOrDefault(variationId, variationId.ToString()[..8])
                    : null,
                rule.ProductVariationId is { } variationTypeId
                    ? variationLookups.TypeNames.GetValueOrDefault(variationTypeId)
                    : null,
                rule.WarehouseId,
                rule.WarehouseId is { } wh ? warehouseNames.GetValueOrDefault(wh, wh.ToString()[..8]) : null,
                rule.LocationId,
                rule.LocationId is { } loc ? locationNames.GetValueOrDefault(loc, loc.ToString()[..8]) : null,
                available,
                reorderPoint,
                rule.MinimumQuantity));
        }

        return rows
            .OrderBy(x => x.ProductName)
            .ThenBy(x => x.WarehouseName)
            .ThenBy(x => x.LocationName)
            .ToList();
    }

    private async Task<List<InventoryReorderRule>> LoadRules(
        Guid tenantId,
        Guid? productId,
        Guid? productVariationId,
        Guid? warehouseId,
        Guid? locationId,
        CancellationToken ct)
    {
        var rules = await dataContext.Query<InventoryReorderRule>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted)
            .ToListAsync(ct);

        if (productId is not null)
            rules = rules.Where(x => x.ProductId == productId).ToList();
        if (productVariationId is not null)
            rules = rules.Where(x => x.ProductVariationId == productVariationId).ToList();
        if (warehouseId is not null)
            rules = rules.Where(x => x.WarehouseId == null || x.WarehouseId == warehouseId).ToList();
        if (locationId is not null)
            rules = rules.Where(x => x.LocationId == null || x.LocationId == locationId).ToList();

        return rules;
    }

    private async Task<VariationLookups> LoadVariationLookups(Guid tenantId, CancellationToken ct)
    {
        var variations = await dataContext.Query<ProductVariation>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(ct);
        var typeIds = variations
            .Where(x => x.ProductVariationTypeId is not null)
            .Select(x => x.ProductVariationTypeId!.Value)
            .Distinct()
            .ToList();
        var types = typeIds.Count == 0
            ? []
            : await dataContext.Query<ProductVariationType>()
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && typeIds.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(ct);
        var typeNames = types.ToDictionary<ProductVariationType, Guid, string?>(
            x => x.Id,
            x => x.Name ?? x.Id.ToString()[..8]);

        return new VariationLookups(
            variations.ToDictionary(x => x.Id, x => x.Name ?? x.Id.ToString()[..8]),
            variations.ToDictionary(
                x => x.Id,
                x => x.ProductVariationTypeId is { } typeId
                    ? typeNames.GetValueOrDefault(typeId, x.VariationType)
                    : x.VariationType));
    }

    private Result<Guid> GetCurrentTenantId(RequestBase? request)
    {
        var tenantId = trustedInvocationContextAccessor.Current?.EffectiveTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result<Guid>.Unauthorized("Authentication is required for inventory planning operations.");
        return Result<Guid>.Success(tenantId.Value);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<Result> EnsurePlanningEnabledAsync(Guid tenantId, CancellationToken ct) =>
        await featureService.EnsureEnabledAsync(
            tenantId,
            TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.PlanningSubFeature,
            ct);

    private sealed record VariationLookups(
        Dictionary<Guid, string> Names,
        Dictionary<Guid, string?> TypeNames);
}
