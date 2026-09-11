using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Lots;

namespace XFramework.Inventario.Api.Services;

using XFramework.Integration.Security;

public sealed class InventoryLotService(
    IDataContext dataContext,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
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
            ReceivedAt = NormalizeUtc(request.ReceivedAt) ?? DateTime.UtcNow,
            ManufacturedAt = NormalizeUtc(request.ManufacturedAt),
            ExpiresAt = NormalizeUtc(request.ExpiresAt),
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
        var tenantId = trustedInvocationContextAccessor.Current?.EffectiveTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result<Guid>.Unauthorized("Authentication is required for lot operations.");
        return Result<Guid>.Success(tenantId.Value);
    }

    private static string? NormalizeRequired(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static DateTime? NormalizeUtc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
    };

    private async Task<Result> EnsureTraceabilityEnabledAsync(Guid tenantId, CancellationToken ct) =>
        await featureService.EnsureEnabledAsync(
            tenantId,
            TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.TraceabilitySubFeature,
            ct);

    public async Task<Result<InventoryLot>> UpdateInventoryLotAsync(UpdateInventoryLotRequest request, CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<InventoryLot>.Failure(tenantResult.Message!, tenantResult.StatusCode);
        var tenantId = tenantResult.Data;
        var featureResult = await EnsureTraceabilityEnabledAsync(tenantId, ct);
        if (!featureResult.IsSuccess)
            return Result<InventoryLot>.Failure(featureResult.Message!, featureResult.StatusCode);
        if (!Enum.IsDefined(request.Status) || NormalizeUtc(request.ManufacturedAt) > NormalizeUtc(request.ExpiresAt))
            return Result<InventoryLot>.Failure("Check lot status and manufacture/expiry dates.", 400);
        if (request.Status != XFramework.Inventario.Domain.Shared.Enums.InventoryLotStatus.Available &&
            await dataContext.Query<StockBalance>().AnyAsync(x => x.TenantId == tenantId && x.LotId == request.Id && x.ReservedQuantity > 0 && !x.IsDeleted, ct))
            return Result<InventoryLot>.Conflict("Release active reservations before changing lot eligibility.");

        if (request.SupplierReference?.Length > 200) return Result<InventoryLot>.Failure("SupplierReference must be at most 200 characters.", 400);
        var entity = await dataContext.Query<InventoryLot>()
            .Where(x => x.TenantId == tenantId && x.Id == request.Id && !x.IsDeleted).FirstOrDefaultAsync(ct);
        if (entity is null) return Result<InventoryLot>.NotFound("Record not found.");
        if (entity.ConcurrencyStamp != request.ConcurrencyStamp)
            return Result<InventoryLot>.Conflict("This record changed. Refresh before saving.");
        dataContext.Update(entity);
        entity.SupplierReference = NormalizeOptional(request.SupplierReference);
        entity.ManufacturedAt = NormalizeUtc(request.ManufacturedAt);
        entity.ExpiresAt = NormalizeUtc(request.ExpiresAt);
        entity.Status = request.Status;
        entity.ModifiedAt = DateTime.UtcNow;
        entity.ConcurrencyStamp = Guid.NewGuid();
        var saved = await dataContext.SaveChangesAsync(ct);
        return saved.IsSuccess ? Result<InventoryLot>.Success(entity) : Result<InventoryLot>.Failure("Could not save changes.", saved.StatusCode);
    }
}
