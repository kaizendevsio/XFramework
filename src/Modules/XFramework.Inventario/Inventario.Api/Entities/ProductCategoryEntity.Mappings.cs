using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// Partial implementation of ProductCategoryEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class ProductCategoryEntityService
{
    /// <summary>
    /// Maps a CreateProductCategoryEntityRequest to a new ProductCategoryEntity.
    /// </summary>
    protected virtual partial ProductCategoryEntity MapCreateRequestToEntity(CreateProductCategoryEntityRequest request)
    {
        return new ProductCategoryEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateProductCategoryEntityRequest to an existing ProductCategoryEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateProductCategoryEntityRequest request, ProductCategoryEntity entity)
    {
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<ProductCategoryEntity> ApplyFilters(IQueryable<ProductCategoryEntity> query, GetProductCategoryEntityListRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(c => 
                (c.Name != null && c.Name.ToLower().Contains(searchLower)) ||
                (c.Description != null && c.Description.ToLower().Contains(searchLower)));
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for ProductCategoryEntity.
/// Implements bidirectional mapping between Domain.Shared.ProductCategory and ProductCategoryEntity.
/// </summary>
public partial class ProductCategoryEntity
{
    /// <summary>
    /// Maps a Domain ProductCategory to a ProductCategoryEntity (VSA wrapper).
    /// </summary>
    public static ProductCategoryEntity FromDomain(ProductCategory domain)
    {
        return new ProductCategoryEntity
        {
            Id = domain.Id,
            Name = domain.Name,
            Description = domain.Description,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this ProductCategoryEntity (VSA wrapper) to a Domain ProductCategory.
    /// </summary>
    public ProductCategory ToDomain()
    {
        return new ProductCategory
        {
            Id = this.Id,
            Name = this.Name,
            Description = this.Description,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}