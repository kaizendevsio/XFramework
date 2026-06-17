namespace XFramework.Inventario.Api.Entities;

/// <summary>
/// VSA wrapper entity for ProductTransaction domain model.
/// Implements the VSA Wrapper Entity Pattern (ADR-003).
/// </summary>
public partial class ProductTransactionEntity
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime TransactionDate { get; set; }
    
    // ISoftDeletable properties
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request DTO for creating a ProductTransactionEntity.
/// </summary>
public class CreateProductTransactionEntityRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime TransactionDate { get; set; }
}

/// <summary>
/// Request DTO for updating a ProductTransactionEntity.
/// </summary>
public class UpdateProductTransactionEntityRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime TransactionDate { get; set; }
}

/// <summary>
/// Request DTO for listing ProductTransactionEntities with pagination.
/// </summary>
public class GetProductTransactionEntityListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? ProductId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
}
