using XFramework.Domain.Generic.Enums;

namespace XFramework.Domain.Generic.BusinessObjects;

public class QueryFilter
{
    public string? PropertyName { get; set; }
    public QueryFilterOperation Operation { get; set; } // e.g., "Equals", "Contains", etc.
    public object? Value { get; set; }
}