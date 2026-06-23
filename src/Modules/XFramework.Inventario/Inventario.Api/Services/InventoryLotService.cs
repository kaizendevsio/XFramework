using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

namespace XFramework.Inventario.Api.Services;

public sealed class InventoryLotService(
    IDataContext dataContext,
    IHttpContextAccessor httpContextAccessor,
    ITenantModuleFeatureService featureService,
    ProductVariationService productVariationService)
{
    public async Task<Result<List<InventoryLot>>> GetLotsAsync(
        GetInventoryLotsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<InventoryLot>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureTraceabilityEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<InventoryLot>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var query = dataContext.Query<InventoryLot>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantResult.Data && !x.IsDeleted);

        if (request.ProductId is { } productId)
            query = query.Where(x => x.ProductId == productId);

        if (request.ProductVariationId is { } productVariationId)
            query = query.Where(x => x.ProductVariationId == productVariationId);

        if (request.Status is { } status)
            query = query.Where(x => x.Status == status);

        if (!request.IncludeExpired)
            query = query.Where(x => x.ExpiresAt == null || x.ExpiresAt >= DateTime.UtcNow);

        var lots = await query
            .OrderBy(x => x.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(x => x.ReceivedAt)
            .ThenBy(x => x.LotNumber)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<InventoryLot>>.Success(lots);
    }

    public async Task<Result<InventoryLot>> GetLotAsync(
        GetInventoryLotRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<InventoryLot>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureTraceabilityEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<InventoryLot>.Failure(featureResult.Message!, featureResult.StatusCode);

        var lot = await dataContext.Query<InventoryLot>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantResult.Data && x.Id == request.Id && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        return lot is null
            ? Result<InventoryLot>.NotFound("Lot not found.")
            : Result<InventoryLot>.Success(lot);
    }

    public async Task<Result<InventoryLot>> CreateLotAsync(
        CreateInventoryLotRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<InventoryLot>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureTraceabilityEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<InventoryLot>.Failure(featureResult.Message!, featureResult.StatusCode);

        var tenantId = tenantResult.Data;
        var lotNumber = NormalizeRequired(request.LotNumber);
        if (lotNumber is null)
            return Result<InventoryLot>.Failure("Lot number is required.", 400);

        var productExists = await dataContext.Query<Product>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == request.ProductId && !x.IsDeleted, ct);
        if (!productExists)
            return Result<InventoryLot>.NotFound("Product not found.");

        var variationResult = await productVariationService.ValidateProductVariationAsync(
            tenantId,
            request.ProductId,
            request.ProductVariationId,
            ct);
        if (!variationResult.IsSuccess)
            return Result<InventoryLot>.Failure(variationResult.Message!, variationResult.StatusCode);

        var duplicate = await dataContext.Query<InventoryLot>()
            .IgnoreQueryFilters()
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ProductId == request.ProductId &&
                x.ProductVariationId == request.ProductVariationId &&
                x.LotNumber == lotNumber &&
                !x.IsDeleted,
                ct);
        if (duplicate)
            return Result<InventoryLot>.Conflict("A lot with the same number already exists for this product and variant.");

        var lot = new InventoryLot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = request.ProductId,
            ProductVariationId = request.ProductVariationId,
            LotNumber = lotNumber,
            SupplierReference = NormalizeOptional(request.SupplierReference),
            SourceReferenceType = NormalizeOptional(request.SourceReferenceType),
            SourceReferenceId = request.SourceReferenceId,
            ReceivedAt = request.ReceivedAt ?? DateTime.UtcNow,
            ManufacturedAt = request.ManufacturedAt,
            ExpiresAt = request.ExpiresAt,
            UnitCost = request.UnitCost,
            Status = request.Status,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(lot);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<InventoryLot>.Failure(saveResult.Message ?? "Lot save failed.", saveResult.StatusCode);

        return Result<InventoryLot>.Success(lot, 201, "Lot created.");
    }

    private Result<Guid> GetCurrentTenantId(RequestBase? request)
    {
        if (request?.Metadata?.TenantId is { } metadataTenantId && metadataTenantId != Guid.Empty)
            return Result<Guid>.Success(metadataTenantId);

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return Result<Guid>.Unauthorized("Authentication is required for lot operations.");

        var tenantIdClaim = user.FindFirst("tenantId")?.Value
            ?? user.FindFirst("TenantId")?.Value
            ?? user.FindFirst("tid")?.Value;

        if (Guid.TryParse(tenantIdClaim, out var tenantId) && tenantId != Guid.Empty)
            return Result<Guid>.Success(tenantId);

        return Result<Guid>.Forbidden("Authenticated user does not have a valid tenant context.");
    }

    private static string? NormalizeRequired(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<Result> EnsureTraceabilityEnabledAsync(Guid tenantId, CancellationToken ct) =>
        await featureService.EnsureEnabledAsync(
            tenantId,
            TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.TraceabilitySubFeature,
            ct);
}
