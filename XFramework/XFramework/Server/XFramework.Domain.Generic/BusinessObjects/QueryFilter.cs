namespace XFramework.Domain.Generic.BusinessObjects;

public class QueryFilter
{
    public string? PropertyName { get; set; }
    public string? Operation { get; set; } // e.g., "Equals", "Contains", etc.
    public string? Value { get; set; }
}