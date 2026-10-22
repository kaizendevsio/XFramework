using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Variations;

namespace XFramework.Inventario.Api.Services;

using XFramework.Integration.Security;

public sealed class ProductVariationService(
    IDataContext dataContext,
    ITrustedInvocationContextAccessor trustedInvocationContextAccessor,
    ITenantModuleFeatureService featureService)
{
    public async Task<Result<List<ProductVariationType>>> GetTypesAsync(
        GetProductVariationTypesRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<ProductVariationType>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureVariationsEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<List<ProductVariationType>>.Failure(featureResult.Message!, featureResult.StatusCode);

        var tenantId = tenantResult.Data;
        var query = dataContext.Query<ProductVariationType>()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (request.ProductId is { } productId)
        {
            query = query.Where(x =>
                request.IncludeTenantWide && x.ProductId == null ||
                request.IncludeProductLocal && x.ProductId == productId);
        }
        else if (!request.IncludeTenantWide || !request.IncludeProductLocal)
        {
            query = query.Where(x =>
                request.IncludeTenantWide && x.ProductId == null ||
                request.IncludeProductLocal && x.ProductId != null);
        }

        var types = await query
            .OrderBy(x => x.ProductId == null ? 0 : 1)
            .ThenBy(x => x.Name)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<ProductVariationType>>.Success(types);
    }

    public async Task<Result<ProductVariationType>> CreateTypeAsync(
        CreateProductVariationTypeRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<ProductVariationType>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureVariationsEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<ProductVariationType>.Failure(featureResult.Message!, featureResult.StatusCode);

        var tenantId = tenantResult.Data;
        var name = NormalizeRequired(request.Name);
        if (name is null)
            return Result<ProductVariationType>.Failure("Variation type name is required.", 400);

        if (request.ProductId is { } productId)
        {
            var productExists = await ProductExistsAsync(tenantId, productId, ct);
            if (!productExists)
                return Result<ProductVariationType>.NotFound("Product not found.");
        }

        var normalizedName = NormalizeKey(name);
        var duplicate = await dataContext.Query<ProductVariationType>()
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ProductId == request.ProductId &&
                x.NormalizedName == normalizedName &&
                !x.IsDeleted,
                ct);
        if (duplicate)
            return Result<ProductVariationType>.Conflict("A variation type with the same name already exists in this scope.");

        var type = new ProductVariationType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            NormalizedName = normalizedName,
            Code = NormalizeOptional(request.Code) ?? normalizedName,
            ProductId = request.ProductId,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(type);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<ProductVariationType>.Failure(saveResult.Message ?? "Variation type save failed.", saveResult.StatusCode);

        return Result<ProductVariationType>.Success(type, 201, "Variation type created.");
    }

    public async Task<Result<ProductVariation>> CreateVariationAsync(
        CreateProductVariationRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<ProductVariation>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureVariationsEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<ProductVariation>.Failure(featureResult.Message!, featureResult.StatusCode);

        var validation = new global::Inventario.Api.Features.Variations.Create.CreateProductVariationValidator().Validate(request);
        if (!validation.IsValid)
            return Result<ProductVariation>.Failure(validation.Errors[0].ErrorMessage, 400);

        var tenantId = tenantResult.Data;
        var product = await LoadProductAsync(tenantId, request.ProductId, ct);
        if (product is null)
            return Result<ProductVariation>.NotFound("Product not found.");

        var definitionResult = await ValidateDefinitionAsync(tenantId, request.ProductId,
            request.ProductVariationTypeId, request.CombinationOptions, ct);
        if (!definitionResult.IsSuccess)
            return Result<ProductVariation>.Failure(definitionResult.Message!, definitionResult.StatusCode);
        var definition = definitionResult.Data!;
        var typeId = definition.TypeId;
        var combinationKey = definition.Key;

        var name = NormalizeRequired(request.Name);
        if (name is null)
            return Result<ProductVariation>.Failure("Variant name is required.", 400);

        var duplicate = await dataContext.Query<ProductVariation>()
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ProductId == request.ProductId &&
                ((x.ProductVariationTypeId == typeId && x.Name == name) ||
                    (combinationKey != null && x.CombinationKey == combinationKey)) &&
                !x.IsDeleted,
                ct);
        if (duplicate)
            return Result<ProductVariation>.Conflict("A variant with the same name and type or option combination already exists for this product.");

        var variation = new ProductVariation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = request.ProductId,
            ProductVariationTypeId = typeId,
            VariationType = definition.TypeName,
            CombinationOptions = definition.Options,
            CombinationKey = combinationKey,
            Name = name,
            Price = request.Price,
            AdditionalPrice = request.Price - product.Price,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(variation);
        var saveResult = await SaveVariationAsync(variation, ct);
        if (!saveResult.IsSuccess)
            return Result<ProductVariation>.Failure(saveResult.Message ?? "Variant save failed.", saveResult.StatusCode);

        return Result<ProductVariation>.Success(variation, 201, "Variant created.");
    }

    public async Task<Result<ProductVariation>> UpdateVariationAsync(
        UpdateProductVariationRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<ProductVariation>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var featureResult = await EnsureVariationsEnabledAsync(tenantResult.Data, ct);
        if (!featureResult.IsSuccess)
            return Result<ProductVariation>.Failure(featureResult.Message!, featureResult.StatusCode);

        var validation = new global::Inventario.Api.Features.Variations.Update.UpdateProductVariationValidator().Validate(request);
        if (!validation.IsValid)
            return Result<ProductVariation>.Failure(validation.Errors[0].ErrorMessage, 400);

        var tenantId = tenantResult.Data;
        var variation = await dataContext.Query<ProductVariation>()
            .Where(x => x.TenantId == tenantId && x.Id == request.ProductVariationId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (variation is null)
            return Result<ProductVariation>.NotFound("Variant not found.");

        if (variation.CombinationOptions is not null && request.CombinationOptions is null)
            return Result<ProductVariation>.Failure("Combination options are required when editing a combination.", 400);
        if (request.ExpectedConcurrencyStamp is { } stamp && stamp != variation.ConcurrencyStamp)
            return Result<ProductVariation>.Conflict("The variant has changed. Reload and try again.");

        var product = await LoadProductAsync(tenantId, variation.ProductId, ct);
        if (product is null)
            return Result<ProductVariation>.NotFound("Product not found.");

        var definitionResult = await ValidateDefinitionAsync(tenantId, variation.ProductId,
            request.ProductVariationTypeId, request.CombinationOptions, ct);
        if (!definitionResult.IsSuccess)
            return Result<ProductVariation>.Failure(definitionResult.Message!, definitionResult.StatusCode);
        var definition = definitionResult.Data!;
        var typeId = definition.TypeId;
        var combinationKey = definition.Key;

        var name = NormalizeRequired(request.Name);
        if (name is null)
            return Result<ProductVariation>.Failure("Variant name is required.", 400);

        var duplicate = await dataContext.Query<ProductVariation>()
            .AnyAsync(x =>
                x.TenantId == tenantId &&
                x.ProductId == variation.ProductId &&
                x.Id != variation.Id &&
                ((x.ProductVariationTypeId == typeId && x.Name == name) ||
                    (combinationKey != null && x.CombinationKey == combinationKey)) &&
                !x.IsDeleted,
                ct);
        if (duplicate)
            return Result<ProductVariation>.Conflict("A variant with the same name and type or option combination already exists for this product.");

        dataContext.Update(variation);
        variation.ProductVariationTypeId = typeId;
        variation.VariationType = definition.TypeName;
        variation.CombinationOptions = definition.Options;
        variation.CombinationKey = combinationKey;
        variation.Name = name;
        variation.Price = request.Price;
        variation.AdditionalPrice = request.Price - product.Price;
        variation.ModifiedAt = DateTime.UtcNow;
        variation.ConcurrencyStamp = Guid.NewGuid();

        var saveResult = await SaveVariationAsync(variation, ct);
        if (!saveResult.IsSuccess)
            return Result<ProductVariation>.Failure(saveResult.Message ?? "Variant update failed.", saveResult.StatusCode);

        return Result<ProductVariation>.Success(variation, "Variant updated.");
    }

    public async Task<Result<ProductVariation?>> ValidateProductVariationAsync(
        Guid tenantId,
        Guid productId,
        Guid? productVariationId,
        CancellationToken ct = default)
    {
        if (productVariationId is null)
            return Result<ProductVariation?>.Success(null);

        var variation = await dataContext.Query<ProductVariation>()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Id == productVariationId.Value &&
                !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (variation is null)
            return Result<ProductVariation?>.NotFound("Variant not found.");

        if (variation.ProductId != productId)
            return Result<ProductVariation?>.Failure("Variant does not belong to the requested product.", 400);

        var productExists = await ProductExistsAsync(tenantId, productId, ct);
        if (!productExists)
            return Result<ProductVariation?>.NotFound("Product not found.");

        return Result<ProductVariation?>.Success(variation);
    }

    private sealed record VariationDefinition(Guid? TypeId, string? TypeName,
        List<ProductVariationOption>? Options, string? Key);

    private async Task<Result<VariationDefinition>> ValidateDefinitionAsync(
        Guid tenantId, Guid productId, Guid typeId, List<ProductVariationOption>? options, CancellationToken ct)
    {
        if (options is null)
        {
            var type = await ValidateVariationTypeAsync(tenantId, productId, typeId, ct);
            return type.IsSuccess
                ? Result<VariationDefinition>.Success(new(typeId, type.Data!.Name, null, null))
                : Result<VariationDefinition>.Failure(type.Message!, type.StatusCode);
        }

        var ids = options.Select(x => x.ProductVariationTypeId).ToList();
        var types = await dataContext.Query<ProductVariationType>()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled && ids.Contains(x.Id) &&
                (x.ProductId == null || x.ProductId == productId))
            .Take(ProductVariationCombination.MaxOptions)
            .ToListAsync(ct);
        if (types.Count != ids.Count)
            return Result<VariationDefinition>.Failure("One or more option types are unavailable for this product.", 400);

        var normalized = ProductVariationCombination.Normalize(options);
        return Result<VariationDefinition>.Success(new(null, "Combination", normalized,
            ProductVariationCombination.CreateKey(normalized)));
    }

    private async Task<Result> SaveVariationAsync(ProductVariation variation, CancellationToken ct)
    {
        var result = await dataContext.SaveChangesAsync(ct);
        if (result.IsSuccess)
            return Result.Success();

        // ServerDataContext hides provider exceptions. Recheck the unique key to translate a losing insert/update race.
        if (variation.CombinationKey is { } key && await dataContext.Query<ProductVariation>()
            .AnyAsync(x => x.TenantId == variation.TenantId && x.ProductId == variation.ProductId &&
                x.Id != variation.Id && x.CombinationKey == key && !x.IsDeleted, ct))
            return Result.Conflict("This option combination already exists for this product.");
        return Result.Failure(result.Message ?? "Variant save failed.", result.StatusCode);
    }

    private async Task<Result<ProductVariationType>> ValidateVariationTypeAsync(
        Guid tenantId,
        Guid productId,
        Guid productVariationTypeId,
        CancellationToken ct)
    {
        var type = await dataContext.Query<ProductVariationType>()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Id == productVariationTypeId &&
                !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (type is null)
            return Result<ProductVariationType>.NotFound("Variation type not found.");

        if (type.ProductId is { } scopedProductId && scopedProductId != productId)
            return Result<ProductVariationType>.Failure("Variation type does not belong to the requested product.", 400);

        return Result<ProductVariationType>.Success(type);
    }

    private async Task<Product?> LoadProductAsync(Guid tenantId, Guid productId, CancellationToken ct) =>
        await dataContext.Query<Product>()
            .Where(x => x.TenantId == tenantId && x.Id == productId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

    private async Task<bool> ProductExistsAsync(Guid tenantId, Guid productId, CancellationToken ct) =>
        await dataContext.Query<Product>()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == productId && !x.IsDeleted, ct);

    private Result<Guid> GetCurrentTenantId(RequestBase? request)
    {
        var tenantId = trustedInvocationContextAccessor.Current?.EffectiveTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return Result<Guid>.Unauthorized("Authentication is required for variant operations.");
        return Result<Guid>.Success(tenantId.Value);
    }

    private async Task<Result> EnsureVariationsEnabledAsync(Guid tenantId, CancellationToken ct) =>
        await featureService.EnsureEnabledAsync(
            tenantId,
            TenantModuleFeatureKeys.Inventario,
            TenantModuleFeatureKeys.VariationsSubFeature,
            ct);

    private static string? NormalizeRequired(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeKey(string value) =>
        value.Trim().ToUpperInvariant();
}
