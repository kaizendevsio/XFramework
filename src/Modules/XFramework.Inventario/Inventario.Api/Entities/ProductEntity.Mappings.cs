using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// Partial implementation of ProductEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class ProductEntityService
{
    /// <summary>
    /// Maps a CreateProductEntityRequest to a new ProductEntity.
    /// </summary>
    protected virtual partial ProductEntity MapCreateRequestToEntity(CreateProductEntityRequest request)
    {
        return new ProductEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            CategoryId = request.CategoryId,
            Image = request.Image,
            SKU = request.SKU,
            Brand = request.Brand,
            Weight = request.Weight,
            DimensionsLength = request.DimensionsLength,
            DimensionsWidth = request.DimensionsWidth,
            DimensionsHeight = request.DimensionsHeight,
            Tags = request.Tags ?? new List<string>(),
            Rating = request.Rating,
            Reviews = request.Reviews ?? new List<string>(),
            Discount = request.Discount,
            IsAvailable = request.IsAvailable,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateProductEntityRequest to an existing ProductEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateProductEntityRequest request, ProductEntity entity)
    {
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Price = request.Price;
        entity.StockQuantity = request.StockQuantity;
        entity.CategoryId = request.CategoryId;
        entity.Image = request.Image;
        entity.SKU = request.SKU;
        entity.Brand = request.Brand;
        entity.Weight = request.Weight;
        entity.DimensionsLength = request.DimensionsLength;
        entity.DimensionsWidth = request.DimensionsWidth;
        entity.DimensionsHeight = request.DimensionsHeight;
        entity.Tags = request.Tags;
        entity.Rating = request.Rating;
        entity.Reviews = request.Reviews;
        entity.Discount = request.Discount;
        entity.IsAvailable = request.IsAvailable;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<ProductEntity> ApplyFilters(IQueryable<ProductEntity> query, GetProductEntityListRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(p => 
                (p.Name != null && p.Name.ToLower().Contains(searchLower)) ||
                (p.Description != null && p.Description.ToLower().Contains(searchLower)) ||
                (p.SKU != null && p.SKU.ToLower().Contains(searchLower)));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }

        if (request.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= request.MinPrice.Value);
        }

        if (request.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= request.MaxPrice.Value);
        }

        if (request.IsAvailable.HasValue)
        {
            query = query.Where(p => p.IsAvailable == request.IsAvailable.Value);
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for ProductEntity.
/// Implements bidirectional mapping between Domain.Shared.Product and ProductEntity.
/// </summary>
public partial class ProductEntity
{
    /// <summary>
    /// Maps a Domain Product to a ProductEntity (VSA wrapper).
    /// </summary>
    public static ProductEntity FromDomain(Product domain)
    {
        return new ProductEntity
        {
            Id = domain.Id,
            Name = domain.Name,
            Description = domain.Description,
            Price = domain.Price,
            StockQuantity = domain.StockQuantity,
            CategoryId = domain.CategoryId,
            Image = domain.Image,
            SKU = domain.SKU,
            Brand = domain.Brand,
            Weight = domain.Weight,
            DimensionsLength = domain.Dimensions?.Length,
            DimensionsWidth = domain.Dimensions?.Width,
            DimensionsHeight = domain.Dimensions?.Height,
            Tags = domain.Tags ?? new List<string>(),
            Rating = domain.Rating,
            Reviews = domain.Reviews ?? new List<string>(),
            Discount = domain.Discount,
            IsAvailable = domain.IsAvailable,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this ProductEntity (VSA wrapper) to a Domain Product.
    /// </summary>
    public Product ToDomain()
    {
        // Create dimensions tuple only if any dimension value exists
        (string Length, string Width, string Height)? dimensions = null;
        if (!string.IsNullOrEmpty(DimensionsLength) ||
            !string.IsNullOrEmpty(DimensionsWidth) ||
            !string.IsNullOrEmpty(DimensionsHeight))
        {
            dimensions = (
                DimensionsLength ?? string.Empty,
                DimensionsWidth ?? string.Empty,
                DimensionsHeight ?? string.Empty
            );
        }

        return new Product
        {
            Id = this.Id,
            Name = this.Name,
            Description = this.Description,
            Price = this.Price,
            StockQuantity = this.StockQuantity,
            CategoryId = this.CategoryId,
            Image = this.Image,
            SKU = this.SKU,
            Brand = this.Brand,
            Weight = this.Weight,
            Dimensions = dimensions,
            Tags = this.Tags ?? new List<string>(),
            Rating = this.Rating,
            Reviews = this.Reviews ?? new List<string>(),
            Discount = this.Discount,
            IsAvailable = this.IsAvailable,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}