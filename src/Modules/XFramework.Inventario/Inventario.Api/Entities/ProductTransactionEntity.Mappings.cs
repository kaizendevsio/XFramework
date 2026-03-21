using XFramework.Inventario.Domain.Shared.Contracts;

namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// Partial implementation of ProductTransactionEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class ProductTransactionEntityService
{
    /// <summary>
    /// Maps a CreateProductTransactionEntityRequest to a new ProductTransactionEntity.
    /// </summary>
    protected virtual partial ProductTransactionEntity MapCreateRequestToEntity(CreateProductTransactionEntityRequest request)
    {
        return new ProductTransactionEntity
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            TotalPrice = request.TotalPrice,
            TransactionDate = request.TransactionDate,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateProductTransactionEntityRequest to an existing ProductTransactionEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateProductTransactionEntityRequest request, ProductTransactionEntity entity)
    {
        entity.ProductId = request.ProductId;
        entity.Quantity = request.Quantity;
        entity.TotalPrice = request.TotalPrice;
        entity.TransactionDate = request.TransactionDate;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<ProductTransactionEntity> ApplyFilters(IQueryable<ProductTransactionEntity> query, GetProductTransactionEntityListRequest request)
    {
        if (request.ProductId.HasValue)
        {
            query = query.Where(t => t.ProductId == request.ProductId.Value);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate <= request.EndDate.Value);
        }

        if (request.MinAmount.HasValue)
        {
            query = query.Where(t => t.TotalPrice >= request.MinAmount.Value);
        }

        if (request.MaxAmount.HasValue)
        {
            query = query.Where(t => t.TotalPrice <= request.MaxAmount.Value);
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for ProductTransactionEntity.
/// Implements bidirectional mapping between Domain.Shared.ProductTransaction and ProductTransactionEntity.
/// </summary>
public partial class ProductTransactionEntity
{
    /// <summary>
    /// Maps a Domain ProductTransaction to a ProductTransactionEntity (VSA wrapper).
    /// </summary>
    public static ProductTransactionEntity FromDomain(ProductTransaction domain)
    {
        return new ProductTransactionEntity
        {
            Id = domain.Id,
            ProductId = domain.ProductId,
            Quantity = domain.Quantity,
            TotalPrice = domain.TotalPrice,
            TransactionDate = domain.TransactionDate,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this ProductTransactionEntity (VSA wrapper) to a Domain ProductTransaction.
    /// </summary>
    public ProductTransaction ToDomain()
    {
        return new ProductTransaction
        {
            Id = this.Id,
            ProductId = this.ProductId,
            Quantity = this.Quantity,
            TotalPrice = this.TotalPrice,
            TransactionDate = this.TransactionDate,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}