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
    ProductVariationService productVariationService,
    DbContext dbContext)
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
                .AnyAsync(x => x.TenantId == tenantId && x.Id == warehouseId && !x.IsDeleted, ct);
            if (!warehouseExists)
                return Result<InventoryReorderRule>.NotFound("Warehouse not found.");
        }

        if (request.LocationId is { } locationId)
        {
            var locationExists = await dataContext.Query<InventoryLocation>()
                .AnyAsync(x =>
                    x.TenantId == tenantId &&
                    x.Id == locationId &&
                    (request.WarehouseId == null || x.WarehouseId == request.WarehouseId) &&
                    !x.IsDeleted, ct);
            if (!locationExists)
                return Result<InventoryReorderRule>.NotFound("Location not found.");
        }

        var duplicate = await dataContext.Query<InventoryReorderRule>()
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
        var page = await LoadReorderSuggestionPageAsync(new()
        {
            Metadata = request.Metadata, ProductId = request.ProductId, ProductVariationId = request.ProductVariationId,
            WarehouseId = request.WarehouseId, LocationId = request.LocationId, Take = 1000
        }, 1000, ct);
        return page.IsSuccess ? Result<List<ReorderSuggestionRow>>.Success(page.Data!.Items)
            : Result<List<ReorderSuggestionRow>>.Failure(page.Message!, page.StatusCode);
    }

    public Task<Result<ReorderSuggestionPage>> GetReorderSuggestionPageAsync(
        GetReorderSuggestionPageRequest request, CancellationToken ct = default)
    {
        var context = trustedInvocationContextAccessor.Current;
        if (context?.Actor is not { } actor || actor.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return Task.FromResult(Result<ReorderSuggestionPage>.Unauthorized("Sign in to view inventory planning."));
        if (!actor.Capabilities.Contains("inventario.planning:view"))
            return Task.FromResult(Result<ReorderSuggestionPage>.Forbidden("Inventory planning view permission is required."));
        if (request.Metadata?.RequestedTenantId is { } target && target != context.EffectiveTenantId)
            return Task.FromResult(Result<ReorderSuggestionPage>.Forbidden("The planning tenant does not match the authorized tenant."));
        return LoadReorderSuggestionPageAsync(request, 500, ct);
    }

    private async Task<Result<ReorderSuggestionPage>> LoadReorderSuggestionPageAsync(
        GetReorderSuggestionPageRequest request, int maximumPageSize, CancellationToken ct)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<ReorderSuggestionPage>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsurePlanningEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<ReorderSuggestionPage>.Failure(featureResult.Message!, featureResult.StatusCode);

        if (request.Skip < 0 || request.Take < 1 || request.Take > maximumPageSize)
            return Result<ReorderSuggestionPage>.Failure($"Choose a page size between 1 and {maximumPageSize} and a nonnegative offset.", 400);
        if (request.WarehouseId is { } warehouseId && !await dataContext.Query<Warehouse>().NoCache()
            .AnyAsync(x => x.TenantId == tenantResult.Data && x.Id == warehouseId && !x.IsDeleted, ct))
            return Result<ReorderSuggestionPage>.NotFound("Warehouse not found.");

        var query = LowStockQuery(tenantResult.Data, request.ProductId, request.ProductVariationId, request.WarehouseId, request.LocationId);
        if (request.SupplierFilterOperator is { } filter)
        {
            var value = request.SupplierFilter ?? "";
            if (value.Length > 200)
                return Result<ReorderSuggestionPage>.Failure("Supplier filter must be at most 200 characters.", 400);
            query = filter switch
            {
                "Equals" => query.Where(x => x.Rule.PreferredSupplier == value),
                "NotEquals" => query.Where(x => x.Rule.PreferredSupplier != value),
                "Contains" => query.Where(x => x.Rule.PreferredSupplier != null && x.Rule.PreferredSupplier.ToLower().Contains(value.ToLower())),
                "NotContains" => query.Where(x => x.Rule.PreferredSupplier == null || !x.Rule.PreferredSupplier.ToLower().Contains(value.ToLower())),
                "StartsWith" => query.Where(x => x.Rule.PreferredSupplier != null && x.Rule.PreferredSupplier.StartsWith(value)),
                "EndsWith" => query.Where(x => x.Rule.PreferredSupplier != null && x.Rule.PreferredSupplier.EndsWith(value)),
                "IsEmpty" => query.Where(x => x.Rule.PreferredSupplier == null || x.Rule.PreferredSupplier == ""),
                "IsNotEmpty" => query.Where(x => x.Rule.PreferredSupplier != null && x.Rule.PreferredSupplier != ""),
                _ => null
            };
            if (query is null)
                return Result<ReorderSuggestionPage>.Failure("Unsupported supplier filter.", 400);
        }
        var count = await query.CountAsync(ct);
        var total = await query.SumAsync(x => (decimal?)Math.Max(x.Rule.ReorderQuantity,
            Math.Max(0m, (x.Rule.MaximumQuantity ?? (x.Rule.ReorderPoint > 0 ? x.Rule.ReorderPoint : x.Rule.MinimumQuantity) + x.Rule.ReorderQuantity) - x.AvailableQuantity)), ct) ?? 0m;
        var positions = await query.OrderBy(x => x.Rule.ProductId).ThenBy(x => x.Rule.Id)
            .Skip(request.Skip).Take(request.Take).ToListAsync(ct);
        var lowStock = await DescribeLowStockRows(tenantResult.Data, positions, ct);

        var suggestions = lowStock.Select((row, index) =>
        {
            var rule = positions[index].Rule;
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

        return Result<ReorderSuggestionPage>.Success(new(count, total, suggestions));
    }

    internal async Task<List<LowStockReportRow>> BuildLowStockRowsAsync(
        Guid tenantId,
        Guid? productId,
        Guid? productVariationId,
        Guid? warehouseId,
        Guid? locationId,
        CancellationToken ct, int skip = 0, int take = 1000)
    {
        var positions = await LowStockQuery(tenantId, productId, productVariationId, warehouseId, locationId)
            .OrderBy(x => x.Rule.ProductId).ThenBy(x => x.Rule.Id).Skip(skip).Take(take).ToListAsync(ct);
        return await DescribeLowStockRows(tenantId, positions, ct);
    }

    private async Task<List<LowStockReportRow>> DescribeLowStockRows(Guid tenantId, List<RuleStock> positions, CancellationToken ct)
    {
        if (positions.Count == 0) return [];
        var rules = positions.Select(x => x.Rule).ToList();

        var productIds = rules.Select(x => x.ProductId).Distinct().ToList();
        var variationIds = rules.Select(x => x.ProductVariationId).OfType<Guid>().Distinct().ToList();
        var warehouseIds = rules.Select(x => x.WarehouseId).OfType<Guid>().Distinct().ToList();
        var locationIds = rules.Select(x => x.LocationId).OfType<Guid>().Distinct().ToList();

        var products = await dataContext.Query<Product>()
            .Where(x => x.TenantId == tenantId && productIds.Contains(x.Id))
            .ToListAsync(ct);
        var variationLookups = await LoadVariationLookups(tenantId, variationIds, ct);
        var warehouses = await dataContext.Query<Warehouse>()
            .Where(x => x.TenantId == tenantId && warehouseIds.Contains(x.Id))
            .ToListAsync(ct);
        var locations = await dataContext.Query<InventoryLocation>()
            .Where(x => x.TenantId == tenantId && locationIds.Contains(x.Id))
            .ToListAsync(ct);

        var productNames = products.ToDictionary(x => x.Id, x => x.Name ?? x.Id.ToString()[..8]);
        var warehouseNames = warehouses.ToDictionary(x => x.Id, x => $"{x.Code} - {x.Name}");
        var locationNames = locations.ToDictionary(x => x.Id, x => $"{x.Code} - {x.Name}");
        var rows = new List<LowStockReportRow>();

        foreach (var position in positions)
        {
            var rule = position.Rule;
            var available = position.AvailableQuantity;
            var reorderPoint = rule.ReorderPoint > 0 ? rule.ReorderPoint : rule.MinimumQuantity;

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

        return rows;
    }

    internal Task<int> CountLowStockAsync(Guid tenantId, Guid? productId, Guid? productVariationId,
        Guid? warehouseId, Guid? locationId, CancellationToken ct) =>
        LowStockQuery(tenantId, productId, productVariationId, warehouseId, locationId).CountAsync(ct);

    private IQueryable<RuleStock> LowStockQuery(Guid tenantId, Guid? productId, Guid? productVariationId,
        Guid? warehouseId, Guid? locationId)
    {
        var rulesQuery = dbContext.Set<InventoryReorderRule>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive);

        if (productId is not null)
            rulesQuery = rulesQuery.Where(x => x.ProductId == productId);
        if (productVariationId is not null)
            rulesQuery = rulesQuery.Where(x => x.ProductVariationId == productVariationId);
        if (warehouseId is not null)
            rulesQuery = rulesQuery.Where(x => x.WarehouseId == null || x.WarehouseId == warehouseId);
        if (locationId is not null)
            rulesQuery = rulesQuery.Where(x => x.LocationId == null || x.LocationId == locationId);

        var balances = dbContext.Set<StockBalance>().AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted &&
            (productVariationId == null || x.ProductVariationId == productVariationId) &&
            (warehouseId == null || x.WarehouseId == warehouseId) && (locationId == null || x.LocationId == locationId));
        // Aggregate the complete stock scope before filtering or paging reorder rules.
        return rulesQuery.Select(rule => new RuleStock
        {
            Rule = rule,
            AvailableQuantity = balances.Where(x => x.ProductId == rule.ProductId &&
                (rule.ProductVariationId == null || x.ProductVariationId == rule.ProductVariationId) &&
                (rule.WarehouseId == null || x.WarehouseId == rule.WarehouseId) &&
                (rule.LocationId == null || x.LocationId == rule.LocationId))
                .Sum(x => (decimal?)x.AvailableQuantity) ?? 0m
        }).Where(x => x.AvailableQuantity <= (x.Rule.ReorderPoint > 0 ? x.Rule.ReorderPoint : x.Rule.MinimumQuantity));
    }

    private sealed class RuleStock
    {
        public InventoryReorderRule Rule { get; init; } = default!;
        public decimal AvailableQuantity { get; init; }
    }

    private async Task<VariationLookups> LoadVariationLookups(
        Guid tenantId,
        IReadOnlyCollection<Guid> variationIds,
        CancellationToken ct)
    {
        if (variationIds.Count == 0)
            return new VariationLookups([], []);

        var variations = await dataContext.Query<ProductVariation>()
            .Where(x => x.TenantId == tenantId && variationIds.Contains(x.Id))
            .ToListAsync(ct);
        var typeIds = variations
            .Where(x => x.ProductVariationTypeId is not null)
            .Select(x => x.ProductVariationTypeId!.Value)
            .Distinct()
            .ToList();
        var types = typeIds.Count == 0
            ? []
            : await dataContext.Query<ProductVariationType>()
                .Where(x => x.TenantId == tenantId && typeIds.Contains(x.Id))
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

    public async Task<Result<InventoryReorderRule>> UpdateInventoryReorderRuleAsync(UpdateInventoryReorderRuleRequest request, CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<InventoryReorderRule>.Failure(tenantResult.Message!, tenantResult.StatusCode);
        var tenantId = tenantResult.Data;
        var featureResult = await EnsurePlanningEnabledAsync(tenantId, ct);
        if (!featureResult.IsSuccess)
            return Result<InventoryReorderRule>.Failure(featureResult.Message!, featureResult.StatusCode);
        if (request.MinimumQuantity < 0 || request.ReorderPoint < 0 || request.ReorderQuantity <= 0 || request.MaximumQuantity < request.MinimumQuantity)
            return Result<InventoryReorderRule>.Failure("Check minimum, maximum, reorder point and reorder quantity.", 400);

        if (request.PreferredSupplier?.Length > 200) return Result<InventoryReorderRule>.Failure("PreferredSupplier must be at most 200 characters.", 400);
        var entity = await dataContext.Query<InventoryReorderRule>()
            .Where(x => x.TenantId == tenantId && x.Id == request.Id && !x.IsDeleted).FirstOrDefaultAsync(ct);
        if (entity is null) return Result<InventoryReorderRule>.NotFound("Record not found.");
        if (entity.ConcurrencyStamp != request.ConcurrencyStamp)
            return Result<InventoryReorderRule>.Conflict("This record changed. Refresh before saving.");
        dataContext.Update(entity);
        entity.MinimumQuantity = request.MinimumQuantity;
        entity.MaximumQuantity = request.MaximumQuantity;
        entity.ReorderPoint = request.ReorderPoint;
        entity.ReorderQuantity = request.ReorderQuantity;
        entity.PreferredSupplier = NormalizeOptional(request.PreferredSupplier);
        entity.IsActive = request.IsActive;
        entity.ModifiedAt = DateTime.UtcNow;
        entity.ConcurrencyStamp = Guid.NewGuid();
        var saved = await dataContext.SaveChangesAsync(ct);
        return saved.IsSuccess ? Result<InventoryReorderRule>.Success(entity) : Result<InventoryReorderRule>.Failure("Could not save changes.", saved.StatusCode);
    }
}
