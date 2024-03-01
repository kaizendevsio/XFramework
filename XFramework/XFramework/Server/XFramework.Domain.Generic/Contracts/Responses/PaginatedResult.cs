namespace XFramework.Domain.Generic.Contracts.Responses;

public record PaginatedResult<T>
(
    long TotalItems,
    int PageIndex,
    int PageSize,
    IEnumerable<T> Items)
{
    public PaginatedResult() : this(0, 0, 0, new List<T>())
    {
        
    }
    public int PageCount => (int)Math.Ceiling(TotalItems / (decimal)PageSize);
}