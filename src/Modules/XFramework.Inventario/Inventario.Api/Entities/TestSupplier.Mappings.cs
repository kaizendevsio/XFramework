namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// Partial implementation of TestSupplierService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class TestSupplierService
{
    /// <summary>
    /// Maps a CreateTestSupplierRequest to a new TestSupplier entity.
    /// </summary>
    protected virtual partial TestSupplier MapCreateRequestToEntity(CreateTestSupplierRequest request)
    {
        return new TestSupplier
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ContactEmail = request.ContactEmail,
            Phone = request.Phone,
            Address = request.Address,
            Website = request.Website,
            IsActive = request.IsActive,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateTestSupplierRequest to an existing TestSupplier entity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateTestSupplierRequest request, TestSupplier entity)
    {
        entity.Name = request.Name;
        entity.ContactEmail = request.ContactEmail;
        entity.Phone = request.Phone;
        entity.Address = request.Address;
        entity.Website = request.Website;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// Override this method to add custom filtering logic.
    /// </summary>
    protected virtual partial IQueryable<TestSupplier> ApplyFilters(IQueryable<TestSupplier> query, GetTestSupplierListRequest request)
    {
        // Default implementation - no additional filters
        // Override to add custom filtering (e.g., search by name, filter by active status, email domain, etc.)
        return query;
    }
}