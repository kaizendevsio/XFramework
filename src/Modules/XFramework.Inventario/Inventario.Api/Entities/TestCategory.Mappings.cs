namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// Partial implementation of TestCategoryService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class TestCategoryService
{
    /// <summary>
    /// Maps a CreateTestCategoryRequest to a new TestCategory entity.
    /// </summary>
    protected virtual partial TestCategory MapCreateRequestToEntity(CreateTestCategoryRequest request)
    {
        return new TestCategory
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateTestCategoryRequest to an existing TestCategory entity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateTestCategoryRequest request, TestCategory entity)
    {
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// Override this method to add custom filtering logic.
    /// </summary>
    protected virtual partial IQueryable<TestCategory> ApplyFilters(IQueryable<TestCategory> query, GetTestCategoryListRequest request)
    {
        // Default implementation - no additional filters
        // Override to add custom filtering (e.g., search by name, filter by active status, etc.)
        return query;
    }
}