namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// Partial implementation of TestProductService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class TestProductService
{
    /// <summary>
    /// Maps a CreateTestProductRequest to a new TestProduct entity.
    /// </summary>
    protected virtual partial TestProduct MapCreateRequestToEntity(CreateTestProductRequest request)
    {
        return new TestProduct
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateTestProductRequest to an existing TestProduct entity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateTestProductRequest request, TestProduct entity)
    {
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Price = request.Price;
        entity.StockQuantity = request.StockQuantity;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// Override this method to add custom filtering logic.
    /// </summary>
    protected virtual partial IQueryable<TestProduct> ApplyFilters(IQueryable<TestProduct> query, GetTestProductListRequest request)
    {
        // Default implementation - no additional filters
        // Override to add custom filtering (e.g., search by name, filter by price range, etc.)
        return query;
    }
}