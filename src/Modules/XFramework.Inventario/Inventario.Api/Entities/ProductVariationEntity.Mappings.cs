using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// Partial implementation of ProductVariationEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class ProductVariationEntityService
{
    /// <summary>
    /// Maps a CreateProductVariationEntityRequest to a new ProductVariationEntity.
    /// </summary>
    protected virtual partial ProductVariationEntity MapCreateRequestToEntity(CreateProductVariationEntityRequest request)
    {
        return new ProductVariationEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            AdditionalPrice = request.AdditionalPrice,
            ProductId = request.ProductId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateProductVariationEntityRequest to an existing ProductVariationEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateProductVariationEntityRequest request, ProductVariationEntity entity)
    {
        entity.Name = request.Name;
        entity.AdditionalPrice = request.AdditionalPrice;
        entity.ProductId = request.ProductId;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<ProductVariationEntity> ApplyFilters(IQueryable<ProductVariationEntity> query, GetProductVariationEntityListRequest request)
    {
        if (request.ProductId.HasValue)
        {
            query = query.Where(v => v.ProductId == request.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(v => v.Name != null && v.Name.ToLower().Contains(searchLower));
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for ProductVariationEntity.
/// Implements bidirectional mapping between Domain.Shared.ProductVariation and ProductVariationEntity.
/// </summary>
public partial class ProductVariationEntity
{
    /// <summary>
    /// Maps a Domain ProductVariation to a ProductVariationEntity (VSA wrapper).
    /// </summary>
    public static ProductVariationEntity FromDomain(ProductVariation domain)
    {
        return new ProductVariationEntity
        {
            Id = domain.Id,
            Name = domain.Name,
            AdditionalPrice = domain.AdditionalPrice,
            ProductId = domain.ProductId,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this ProductVariationEntity (VSA wrapper) to a Domain ProductVariation.
    /// </summary>
    public ProductVariation ToDomain()
    {
        return new ProductVariation
        {
            Id = this.Id,
            Name = this.Name,
            AdditionalPrice = this.AdditionalPrice,
            ProductId = this.ProductId,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}